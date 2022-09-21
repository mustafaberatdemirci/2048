using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameState 
{
    public int[,] tileValues = new int[TileManager.GridSize, TileManager.GridSize];
    public int score;
    public int moveCount;
}
