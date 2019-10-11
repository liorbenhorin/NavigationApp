﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Animations;
using CoordinateSharp;
using RTG;

struct FlightLegData
{
    public GameObject leg;
    public GameObject start;
    public GameObject end;
    public Leg script;
}

public class Main : MonoBehaviour
{

    //public GameObject crosshair;
    public GameObject waypoint;
    public GameObject leg;
    public GameObject mainCamera;
    public GameObject buttonDraw;
    public float dragSpeed = 20;
    public int camZoomStep = 5;
    public Tut_1_Enabling_and_Disabling_Gizmos gizmos;
    public Texture2D cursorTextureDrag;
    public Texture2D cursorTextureDragAction;
    public CursorMode cursorMode = CursorMode.Auto;
    public Vector2 hotSpot = Vector2.zero;
    public DataIO dataIO;

    public GameObject exportCamera;

    // Start is called before the first frame update
    private List<FlightLegData> flight = new List<FlightLegData>();
    private List<GameObject> waypoints = new List<GameObject>();
    private Vector3 lastMouse;
    private Transform mapCameraTransform;
    private Camera mapCameraCamera;
    private Transform exportCameraTransform;
    private Camera exportCameraCamera;
    private float zoom;
    private bool canZoom;

    private bool snapshot;


    // static varibales to define the world scale
    private static double lonConversionRate = 8.392355; //of NM=0.16666667 degrees along Longatiute
    private static double latConversionRate = 10.00674; //of NM=0.16666667 degrees along Latitude
    private static double NMConversionRate = 0.1666667;
    private static double lonOriginRadians = 35d;
    private static double latOriginRadians = 33d;

    private bool resumeDrawing;

    public double flightSpeed = 90d; //TODO: Will be on the leg level

    public Toolbar toolbar;

    private void Start()
    {
        // TODO: is this in fact the correct way to optimize for WebGL?
        Application.targetFrameRate = 24;

        mapCameraTransform = mainCamera.transform;
        mapCameraCamera = mainCamera.GetComponent<Camera>();

        exportCameraTransform = exportCamera.transform;
        exportCameraCamera = exportCamera.GetComponent<Camera>();

        toolbar.eventDrawing.AddListener(SetStartDrawing);
        toolbar.eventStopDrawing.AddListener(SetStopDrawing);
        toolbar.eventActionTriggered.AddListener(DoAction);

    }


    public static int ToMagnetic(int angle = 360, int divation = -4)
    {
        int result = angle + divation;
        if (result < 0)
        {
            result += 360;
        }

        return result;
    }

    public static string ToHMS(double time)
    {
        var result = TimeSpan.FromHours(time);
        var seconds = (int)Math.Round(result.Seconds / 5.0) * 5;
        return result.Minutes + ":" + seconds.ToString("D2"); //result.Hours + ": " +  Only return minutes seconds
    }

    public static Coordinate SceneToCoordinate(Vector3 cursorPosition)
    {
        // x --> lon (presented as NM dist from 35E lon)
        // z --> lat (presented as NM dist from 33N lat)

        Vector3 objectPos = cursorPosition;
        double lon = ((objectPos.x / lonConversionRate) * NMConversionRate) + lonOriginRadians;
        double lat = ((objectPos.z / latConversionRate) * NMConversionRate) + latOriginRadians;
        Coordinate c = new Coordinate(lat, lon);
        //print("lon.x: " + objectPos.x + " lat.y: " + objectPos.z);
        //print("lon: " + (objectPos.x / lonConversionRate) + " lat: " + (objectPos.z / latConversionRate));
        //print("lon: " + lon + " lat: " + lat);
        //print(c);
        return c;
    }

    public Vector3 CursorLocalPosition()
    {
        var mousePos = Input.mousePosition;
        mousePos.z = 5f; // we want 2m away from the camera position
        return Camera.main.ScreenToWorldPoint(mousePos);
    }

    public void SetStartDrawing()
    {
        if (WaypointsExists())
        {
            resumeDrawing = true;
        }
    }

    private void SetStopDrawing()
    {
        if (WaypointsExists())
        {
            RemoveLastWaypoint();
            gizmos.OnTargetObjectChanged(null);
        }
        
    }

    private bool WaypointsExists()
    {
        if (waypoints.Count > 0) return true;
        return false;
    }

    private GameObject CreateWaypoint(Vector3 pos)
    {
        GameObject wp = Instantiate(waypoint, pos, Quaternion.identity);
        waypoints.Add(wp);
        wp.name = "Waypoint_" + waypoints.Count;
        
        return wp;
    }

    private void DoAction(ToolType tool)
    {
        SceneData scene;

        switch(tool)
        {
            case ToolType.Reverse:
                waypoints.Reverse();
                DrawLegs();
                break;

            case ToolType.NewScene:
                Cleanup();
                break;

            case ToolType.SaveScene:
                scene = new SceneData(waypoints);
                dataIO.Download(scene);
                break;

            case ToolType.LoadScene:
                dataIO.eventUpload.AddListener(OnUploadSceneData);
                dataIO.Upload();
                break;

            case ToolType.Print:
                snapshot = true;
                // var p = Application.persistentDataPath + "/snap.png";
                // ScreenCapture.CaptureScreenshot(p, 1);
                break;    
        }
    }

    private void OnUploadSceneData(string json)
    {
        Cleanup();

        SceneData scene = JsonUtility.FromJson<SceneData>(json);
    
        foreach(Position p in scene.waypoints)
        {
            CreateWaypoint(new Vector3(p.x, p.y, p.z));
        }
        DrawLegs();
    }

    private void Cleanup()
    {
        while (flight.Count > 0)
        {
            Destroy(flight[flight.Count - 1].leg);
            flight.RemoveAt(flight.Count - 1);
        }

        while (waypoints.Count > 0)
        {
            Destroy(waypoints[waypoints.Count - 1]);
            waypoints.RemoveAt(waypoints.Count - 1);
        }

        DrawLegs();
    }

    void RemoveLastWaypoint()
    {
        if (waypoints.Count > 0)
        {
            Destroy(waypoints[waypoints.Count-1]);
            waypoints.RemoveAt(waypoints.Count-1);
            DrawLegs();
        }
    }

    void DrawLegs()
    {   
        // to be called when waypoint count is changed!
        if (waypoints.Count > 1)
        {
            for (int i=0; i<waypoints.Count-1; i++)
            {
                if (flight.Count>i)
                {
                    var curLeg = flight[i];
                    if (curLeg.start != waypoints[i] || curLeg.end != waypoints[i+1])
                    {
                        // this leg needs to be fixed
                        curLeg.script.startSource = waypoints[i];
                        curLeg.script.endSource = waypoints[i+1];
                        curLeg.script.startWaypoint = waypoints[i].GetComponent<Waypoint>();
                        curLeg.script.endWaypoint = waypoints[i+1].GetComponent<Waypoint>();
                        curLeg.start = waypoints[i];
                        curLeg.end = waypoints[i+1];
                        curLeg.script.dirty = true;

                        flight[i] = curLeg; //cast the updated leg back into the list
                    }
                }
                if (flight.Count <= i)
                {
                    FlightLegData l = new FlightLegData();
                    
                    l.leg = UnityEngine.Object.Instantiate(leg, Vector3.zero, Quaternion.identity);
                    
                    l.script = l.leg.GetComponent<Leg>();
                    l.script.startSource = waypoints[i];
                    l.script.endSource = waypoints[i+1];
                    l.script.startWaypoint = waypoints[i].GetComponent<Waypoint>();
                    l.script.endWaypoint = waypoints[i+1].GetComponent<Waypoint>();
                    l.leg.GetComponent<LineRenderer>().enabled = true;
                    l.start = waypoints[i];
                    l.end = waypoints[i+1];

                    flight.Add(l);
                    l.leg.name = "Leg_" + flight.Count;
                }
            }    
        }
        
        while (flight.Count > Math.Max(waypoints.Count-1,0))
        {
            Destroy(flight[flight.Count-1].leg);
            flight.RemoveAt(flight.Count-1);
        } 

    }


    // Update is called once per frame
    void Update()
    {
        //if (!Input.GetKey(KeyCode.Space)) Cursor.SetCursor(null, Vector2.zero, cursorMode);    
        
        var objectPos = CursorLocalPosition();
        // TODO: edit mode, delete mode

        //crosshair.transform.position = objectPos;

        
        if (toolbar.currentTool != ToolType.None && Input.GetKeyDown(KeyCode.Escape))
        {
            toolbar.StopAll();
            return;
        }

        ////////////////////////////////////////////////////////////
        // Screen navigation logic /////////////////////////////////
        if (Input.GetAxis("Mouse ScrollWheel") > 0 && zoom > 5)
        {
            zoom -= camZoomStep;
            canZoom = true;
        }

        if (Input.GetAxis("Mouse ScrollWheel") < 0 && zoom < 100)
        {
            zoom += camZoomStep;
            canZoom = true;
        }

        if (canZoom)
        {

            mapCameraCamera.orthographicSize = zoom;
        }


        if (Input.GetKey(KeyCode.Space))
        {
            //Cursor.SetCursor(cursorTextureDrag, hotSpot, cursorMode); 
            
            if (Input.GetMouseButtonDown(0))
            {

                lastMouse = Input.mousePosition;
                return;
            }

            if (Input.GetMouseButton(0))
            {
                //Cursor.SetCursor(cursorTextureDragAction, hotSpot, cursorMode);

                var curMouse = Input.mousePosition;
                var pos = Camera.main.ScreenToViewportPoint(lastMouse - curMouse);
                var orthographicSize = mapCameraCamera.orthographicSize;
                var move = new Vector3(pos.x * dragSpeed * (orthographicSize / 10), 0,
                    pos.y * dragSpeed * (orthographicSize / 10));
                lastMouse = curMouse;
                mapCameraTransform.Translate(move, Space.World);
                return;
            }
        }

        ////////////////////////////////////////////////////////////
        // End of Screen navigation logic //////////////////////////

        switch (toolbar.currentTool)
        {
            case ToolType.Draw:
            {
                // Draw mode:
                if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject()) // mouse left was clicked
                {
                    GameObject wp1;
                    if (!WaypointsExists())
                    {
                        wp1 = CreateWaypoint(objectPos);
                    }
                    else
                    {
                        wp1 = waypoints[waypoints.Count - 1];
                    }
                    
                    var wp2 = CreateWaypoint(objectPos);
                    DrawLegs();
                }
                else if (resumeDrawing)
                {
                    var wp1 = waypoints[waypoints.Count - 1];
                    var wp2 = CreateWaypoint(wp1.transform.position);
                    DrawLegs();
                    resumeDrawing = false;
                }
                
                // mouse in motion with the next waypoint...
                if (!WaypointsExists()) return;
                waypoints[waypoints.Count - 1].transform.position = objectPos;
                break;

            }

            case ToolType.Erase: 
            {
                if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject()) // mouse left was clicked
                {
                    Ray ray = Camera.main.ScreenPointToRay( Input.mousePosition );
                    RaycastHit hit;
                    
                    if( Physics.Raycast( ray, out hit, 100 ) )
                    {
                        GameObject o = hit.transform.gameObject;
                        waypoints.Remove(o);
                        gizmos.OnTargetObjectChanged(null);
                        Destroy(o);
                        DrawLegs();
                    }
                }
                break;
            }

            case ToolType.None: 
            {
                return;
            }

        }

    }

    void LateUpdate()
    {
        if (snapshot)
        {
            exportCamera.SetActive(true);

            //exportCameraTransform.position = mapCameraTransform.position;
            exportCameraCamera.orthographicSize = mapCameraCamera.orthographicSize;

            int resWidth = 2895;
            int resHeight = 4096;

            RenderTexture rt = new RenderTexture(resHeight, resHeight, 24);
            exportCameraCamera.targetTexture = rt;
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            exportCameraCamera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            exportCameraCamera.targetTexture = null;
            RenderTexture.active = null; // JC: added to avoid errors
            Destroy(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            string filename = Application.persistentDataPath + "/" + "snap.png";
            System.IO.File.WriteAllBytes(filename, bytes);
            Debug.Log("Snapshot taken to - " + filename);
            snapshot = false;
        }
    }

}

