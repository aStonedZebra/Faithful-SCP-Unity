﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Event_Parent : MonoBehaviour
{
    public bool isStarted = false;
    public int x, y, state;
    // Start is called before the first frame update
    public virtual void EventLoad()
    {
    }
    public virtual void EventUnLoad()
    {
    }

    public virtual void EventStart()
    {
        Debug.Log("Empezando Evento");
        isStarted = true;
    }
    public virtual void EventUpdate()
    {
    }
    public virtual void EventFinished()
    {
        Debug.Log("Evento Marcado como termninado. X " + x + " Y " + y);
        GameController.instance.setDone(x, y);
    }
}
