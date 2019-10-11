﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using SFB;


public class UploadEvent:UnityEvent<string>
{

}


public class DataIO : MonoBehaviour {
    public UploadEvent eventUpload = new UploadEvent();

    #if UNITY_WEBGL && !UNITY_EDITOR
    //
    // WebGL
    //
    [DllImport("__Internal")]
    private static extern void UploadFile(string gameObjectName, string methodName, string filter, bool multiple);
    [DllImport("__Internal")]
    private static extern void DownloadFile(string gameObjectName, string methodName, string filename, byte[] byteArray, int byteArraySize);


    public void Upload() {
        UploadFile(gameObject.name, "OnFileUpload", ".json", false);
    }

    private void _Download(string _data) {
        var bytes = Encoding.UTF8.GetBytes(_data);
        DownloadFile(gameObject.name, "OnFileDownload", "sample.json", bytes, bytes.Length);
    }

    // Called from browser
    public void OnFileUpload(string url) {
        StartCoroutine(OutputRoutine(url));
    }

    public void OnFileDownload() {
    }

    #else
    
    public void Upload() {
        var paths = StandaloneFileBrowser.OpenFilePanel("Title", "", "json", false);
        if (paths.Length > 0) {
            var url = new System.Uri(paths[0]).AbsoluteUri;
            StartCoroutine(OutputRoutine(url));
        }
    }

    private void _Download(string _data)
    {
        var path = StandaloneFileBrowser.SaveFilePanel("Title", "", "sample", "json");
        if (!string.IsNullOrEmpty(path)) {
            File.WriteAllText(path, _data);
        }
    }

    #endif

    public void Download(SceneData scene)
    {
        string json = JsonUtility.ToJson(scene);
        _Download(json);
    }

    private IEnumerator OutputRoutine(string url) {
        var loader = new WWW(url);
        yield return loader;
        Debug.Log(loader.text);
        eventUpload.Invoke(loader.text);
    }

}