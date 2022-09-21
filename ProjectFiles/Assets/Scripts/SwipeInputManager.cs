using System;
using Unity.Mathematics;
using UnityEngine;

public class SwipeInputManager : IInputManager
{
    private bool _isSwiping = false;
    private Vector3 _startPos;

    private const int MinSwipeDist = 100;

    public InputResult GetInput()
    {
        InputResult result = new InputResult();
        if (!_isSwiping)
        {
            if (Input.GetMouseButton(0))
            {
                _isSwiping = true;
                _startPos = Input.mousePosition;
            }
        }
        else
        {
            if (!Input.GetMouseButton(0))
            {
                _isSwiping = false;
                Vector3 delta = Input.mousePosition - _startPos;
                if (delta.magnitude>= MinSwipeDist)
                {
                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    {
                        result.XInput = Math.Sign(delta.x);
                    }
                    else
                    {
                        result.YInput = Math.Sign(delta.y);
                    }
                }
            }
        }

        return result;
    }
}