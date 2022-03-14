using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public bool cursorLock = true;
    public bool rebuildSVO;

    void Update()
    {
        GlobalIllumination.rebuildSVO = rebuildSVO;
        InternalLockUpdate();
    }

    private void InternalLockUpdate()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl)) cursorLock = !cursorLock;
        if (cursorLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (!cursorLock)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

}
