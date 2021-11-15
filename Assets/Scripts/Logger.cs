using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;


public class Logger : MonoBehaviour
{
    [SerializeReference] private List<LogType> _logTypes = new List<LogType>();
    [SerializeField] private int _numberOfLines = 10;

    private readonly Queue<string> _logQueue = new Queue<string>();
    private TextMeshProUGUI _tmp;

    private void Awake()
    {
        _tmp = GetComponentInChildren<TextMeshProUGUI>();
        Application.logMessageReceived += HandleLog;
    }


    private void OnEnable()
    {
        _tmp.text = string.Join("\n", _logQueue);
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (!_logTypes.Contains(type)) return;

        _logQueue.Enqueue(logString);

        if (_logQueue.Count > _numberOfLines)
        {
            _logQueue.Dequeue();
        }

        if (isActiveAndEnabled)
        {
            _tmp.text = string.Join("\n", _logQueue);
        }
    }
}