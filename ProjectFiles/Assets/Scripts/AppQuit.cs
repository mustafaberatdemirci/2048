using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class AppQuit : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    public void appQuit()
    {
        if(Input.GetMouseButtonDown(0))
        {
            Debug.Log("app quit");
            Application.Quit();
        }
    }
}