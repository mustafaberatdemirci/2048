using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class TileManager : MonoBehaviour
{
    public static int GridSize = 4;

    private readonly Transform[,] _tilePositions = new Transform[GridSize, GridSize];

    private readonly Tile[,] _tiles = new Tile[GridSize, GridSize];

    [SerializeField] private Tile tilePrefab;
    private bool _isAnimating;
    [SerializeField] private TileSettings tileSettings;
    [SerializeField] private UnityEvent<int> scoreUpdated;
    [SerializeField] private UnityEvent<int> bestScoreUpdated;
    [SerializeField] private UnityEvent<int> moveCountUpdated;
    [SerializeField] private UnityEvent<System.TimeSpan> gameTimeUpdated;
    [SerializeField] private GameOverScreen _gameOverScreen;
    private Stack<GameState> _gameStates = new Stack<GameState>();
    private Stopwatch _gameStopwatch = new Stopwatch();
    private IInputManager _inputManager = new MultipleInputManager(new KeyboardInputManager(),new SwipeInputManager());
    private int _score;
    private int _bestScore;
    private int _moveCount;

    // Start is called before the first frame update
    void Start()
    {
        GetTilePositions();
        TrySpawnTile();
        TrySpawnTile();
        UpdateTilePositions(true);
        _gameStopwatch.Start();
        _bestScore = PlayerPrefs.GetInt("Best Score", 0);
        bestScoreUpdated.Invoke(_bestScore);
        /*
        for (int x = 0; x < GridSize; x++)
            for (int y = 0; y < GridSize; y++)
                    Debug.Log($" {x} , {y} {_tilePositions[x,y].position} ");*/
    }
    
    public void RestartGame()
    {
        var activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }

    public void AddScore(int value)
    {
        _score += value;
        scoreUpdated.Invoke(_score);
        if (_score > _bestScore)
        {
            _bestScore = _score;
            bestScoreUpdated.Invoke(_bestScore);
            PlayerPrefs.SetInt("Best Score", _bestScore);
        }
    }

    // Update is called once per frame
    void Update()
    {
        gameTimeUpdated.Invoke(_gameStopwatch.Elapsed);

        //Debug.Log($"Input {xInput} , {yInput} ");

        InputResult input = _inputManager.GetInput();
        
        if (!_isAnimating)
        {
            TryMove(input.XInput, input.YInput);
        }
        //_lastXInput = xInput;
        //_lastYInput = yInput;
    }

    private void GetTilePositions()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        int x = 0;
        int y = 0;
        foreach (Transform transform in this.transform)
        {
            _tilePositions[x, y] = transform;
            x++;
            if (x >= GridSize)
            {
                x = 0;
                y++;
            }
        }
    }

    private bool TrySpawnTile()
    {
        List<Vector2Int> availableSpots = new List<Vector2Int>();
        for (int x = 0; x < GridSize; x++)
        for (int y = 0; y < GridSize; y++)
        {
            if (_tiles[x, y] == null)
            {
                availableSpots.Add(new Vector2Int(x, y));
            }
        }

        if (!availableSpots.Any())
        {
            return false;
        }

        int randomIndex = Random.Range(0, availableSpots.Count);
        Vector2Int spot = availableSpots[randomIndex];

        var tile = Instantiate(tilePrefab, transform.parent);
        tile.SetValue(GetRandomValue());
        _tiles[spot.x, spot.y] = tile;

        return true;
    }

    private int GetRandomValue()
    {
        var rand = Random.Range(0f, 1f);
        if (rand <= .8f)
        {
            return 2;
        }
        else
        {
            return 4;
        }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private void UpdateTilePositions(bool instant)
    {
        if (!instant)
        {
            _isAnimating = true;
            StartCoroutine(WaitForTileAnimation());
        }

        for (int x = 0; x < GridSize; x++)
        for (int y = 0; y < GridSize; y++)
            if (_tiles[x, y] != null)
            {
                _tiles[x, y].SetPosition(_tilePositions[x, y].position, instant);
            }
    }

    private IEnumerator WaitForTileAnimation()
    {
        yield return new WaitForSeconds(tileSettings.AnimationTime);
        if (!TrySpawnTile())
        {
            Debug.LogError("UNABLE TO SPAWN TILE");
        }

        UpdateTilePositions(true);
        if (!anyMovesLeft())
        {
            _gameOverScreen.SetGameOver(true);
        }

        _isAnimating = false;
    }

    private bool anyMovesLeft()
    {
        return CanMoveLeft() || CanMoveRight() || CanMoveUp() || CanMoveDown();
    }

    private bool _tilesUpdated;


    private void TryMove(int x, int y)
    {
        if (x == 0 && y == 0)
        {
            return;
        }

        if (Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1)
        {
            Debug.LogWarning($"Invalid move {x} , {y} ");
            return;
        }

        _tilesUpdated = false;
        int[,] preMoveTileValues = GetCurrentTileValues();
        if (x == 0)
        {
            if (y > 0)
            {
                TryMoveUp();
            }
            else
            {
                TryMoveDown();
            }
        }
        else
        {
            if (x < 0)
                TryMoveLeft();
            else
                TryMoveRight();
        }

        if (_tilesUpdated)
        {
            _gameStates.Push(new GameState()
                { tileValues = preMoveTileValues, score = _score, moveCount = _moveCount });
            _moveCount++;
            moveCountUpdated.Invoke(_moveCount);
            UpdateTilePositions(false);
        }
    }

    private int[,] GetCurrentTileValues()
    {
        int[,] result = new int[GridSize, GridSize];
        for (int x = 0; x < GridSize; x++)
        for (int y = 0; y < GridSize; y++)
            if (_tiles[x, y] != null)
            {
                result[x, y] = _tiles[x, y].GetValue();
            }

        return result;
    }

    public void LoadLastGameStates()
    {
        if (_isAnimating)
        {
            return;
        }

        if (!_gameStates.Any())
        {
            return;
        }

        GameState previousGameState = _gameStates.Pop();
        _gameOverScreen.SetGameOver(false);
        _score = previousGameState.score;
        scoreUpdated.Invoke(_score);
        _moveCount = previousGameState.moveCount;
        moveCountUpdated.Invoke(_moveCount);
        foreach (Tile t in _tiles)
        {
            if (t != null)
            {
                Destroy(t.gameObject);
            }
        }

        for (int x = 0; x < GridSize; x++)
        for (int y = 0; y < GridSize; y++)
        {
            _tiles[x, y] = null;
            if (previousGameState.tileValues[x, y] == 0)
            {
                continue;
            }

            Tile tile = Instantiate(tilePrefab, transform.parent);
            tile.SetValue(previousGameState.tileValues[x, y]);
            _tiles[x, y] = tile;
        }

        UpdateTilePositions(true);
    }

    private bool TileExistsBetween(int x, int y, int x2, int y2)
    {
        if (x == x2)
            return TileExistsBetweenVertical(x, y, y2);
        else if (y == y2)
            return TileExistsBetweenHorizontal(x, x2, y);
        Debug.LogError($"BETWEEN CHECK - INVALID PARAMETERS ({x},{y})({x2},{y2})");
        return true;
    }

    private bool TileExistsBetweenHorizontal(int x, int x2, int y)
    {
        int minX = Mathf.Min(x, x2);
        int maxX = Mathf.Max(x, x2);
        for (int xIndex = minX + 1; xIndex < maxX; xIndex++)
            if (_tiles[xIndex, y] != null)
                return true;
        return false;
    }

    private bool TileExistsBetweenVertical(int x, int y, int y2)
    {
        int minY = Mathf.Min(y, y2);
        int maxY = Mathf.Max(y, y2);
        for (int yIndex = minY + 1; yIndex < maxY; yIndex++)
            if (_tiles[x, yIndex] != null)
                return true;
        return false;
    }

    private void TryMoveRight()
    {
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = GridSize - 1; x >= 0; x--)
            {
                if (_tiles[x, y] == null)
                {
                    continue;
                }

                for (int x2 = GridSize - 1; x2 > x; x2--)
                {
                    if (_tiles[x2, y] != null)
                    {
                        if (TileExistsBetween(x, y, x2, y))
                            continue;
                        if (_tiles[x2, y].Merge(_tiles[x, y]))
                        {
                            _tiles[x, y] = null;
                            _tilesUpdated = true;
                            break;
                        }

                        continue;
                    }

                    _tilesUpdated = true;
                    _tiles[x2, y] = _tiles[x, y];
                    _tiles[x, y] = null;
                    break;
                }
            }
        }
    }

    private void TryMoveLeft()
    {
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                if (_tiles[x, y] == null) continue;
                for (int x2 = 0; x2 < x; x2++)
                {
                    if (_tiles[x2, y] != null)
                    {
                        if (TileExistsBetween(x, y, x2, y))
                            continue;
                        if (_tiles[x2, y].Merge(_tiles[x, y]))
                        {
                            _tiles[x, y] = null;
                            _tilesUpdated = true;
                            break;
                        }

                        continue;
                    }

                    _tilesUpdated = true;
                    _tiles[x2, y] = _tiles[x, y];
                    _tiles[x, y] = null;
                    break;
                }
            }
        }
    }

    private void TryMoveDown()
    {
        for (int x = 0; x < GridSize; x++)
        {
            for (int y = GridSize - 1; y >= 0; y--)
            {
                if (_tiles[x, y] == null) continue;
                for (int y2 = GridSize - 1; y2 > y; y2--)
                {
                    if (_tiles[x, y2] != null)
                    {
                        if (TileExistsBetween(x, y, x, y2))
                            continue;
                        if (_tiles[x, y2].Merge(_tiles[x, y]))
                        {
                            _tiles[x, y] = null;
                            _tilesUpdated = true;
                            break;
                        }

                        continue;
                    }

                    _tilesUpdated = true;
                    _tiles[x, y2] = _tiles[x, y];
                    _tiles[x, y] = null;
                    break;
                }
            }
        }
    }

    private void TryMoveUp()
    {
        for (int x = 0; x < GridSize; x++)
        {
            for (int y = 0; y < GridSize; y++)
            {
                if (_tiles[x, y] == null) continue;
                for (int y2 = 0; y2 < y; y2++)
                {
                    if (_tiles[x, y2] != null)
                    {
                        if (TileExistsBetween(x, y, x, y2))
                            continue;
                        if (_tiles[x, y2].Merge(_tiles[x, y]))
                        {
                            _tiles[x, y] = null;
                            _tilesUpdated = true;
                            break;
                        }

                        continue;
                    }

                    _tilesUpdated = true;
                    _tiles[x, y2] = _tiles[x, y];
                    _tiles[x, y] = null;
                    break;
                }
            }
        }
    }

    private bool CanMoveRight()
    {
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = GridSize - 1; x >= 0; x--)
            {
                if (_tiles[x, y] == null)
                {
                    continue;
                }

                for (int x2 = GridSize - 1; x2 > x; x2--)
                {
                    if (_tiles[x2, y] != null)
                    {
                        if (TileExistsBetween(x, y, x2, y))
                            continue;
                        if (_tiles[x2, y].CanMerge(_tiles[x, y]))
                        {
                            return true;
                        }

                        continue;
                    }

                    return true;
                }
            }
        }

        return false;
    }

    private bool CanMoveLeft()
    {
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                if (_tiles[x, y] == null) continue;
                for (int x2 = 0; x2 < x; x2++)
                {
                    if (_tiles[x2, y] != null)
                    {
                        if (TileExistsBetween(x, y, x2, y))
                            continue;
                        if (_tiles[x2, y].CanMerge(_tiles[x, y]))
                        {
                            return true;
                        }

                        continue;
                    }

                    return true;
                }
            }
        }

        return false;
    }

    private bool CanMoveDown()
    {
        for (int x = 0; x < GridSize; x++)
        {
            for (int y = GridSize - 1; y >= 0; y--)
            {
                if (_tiles[x, y] == null) continue;
                for (int y2 = GridSize - 1; y2 > y; y2--)
                {
                    if (_tiles[x, y2] != null)
                    {
                        if (TileExistsBetween(x, y, x, y2))
                            continue;
                        if (_tiles[x, y2].CanMerge(_tiles[x, y]))
                        {
                            return true;
                        }

                        continue;
                    }

                    return true;
                }
            }
        }

        return false;
    }

    private bool CanMoveUp()
    {
        for (int x = 0; x < GridSize; x++)
        {
            for (int y = 0; y < GridSize; y++)
            {
                if (_tiles[x, y] == null) continue;
                for (int y2 = 0; y2 < y; y2++)
                {
                    if (_tiles[x, y2] != null)
                    {
                        if (TileExistsBetween(x, y, x, y2))
                            continue;
                        if (_tiles[x, y2].CanMerge(_tiles[x, y]))
                        {
                            return true;
                        }

                        continue;
                    }

                    return true;
                }
            }
        }

        return false;
    }
}