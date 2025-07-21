using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_ResolutionHandler : MonoBehaviour
{
    public List<CanvasScaler> canvasScalers;

    public float currentDeviceResolution;

    private void Awake()
    {
        SetResolution();
        SetAllcanvasScaler();
    }

    private void Update()
    {
        if(currentDeviceResolution != (Screen.height / (float)Screen.width))
        {
            SetResolution();
            SetAllcanvasScaler();
        }
    }

    void SetAllcanvasScaler()
    {
        if(currentDeviceResolution>2.17f)
        {
            foreach (var item in canvasScalers)
            {
                item.matchWidthOrHeight = 0;
            }
        }
        else
        {
            foreach (var item in canvasScalers)
            {
                item.matchWidthOrHeight = 1;
            }
        }
    }

    void SetResolution()
    {
        currentDeviceResolution = Screen.height / (float)Screen.width; 
    }
}
