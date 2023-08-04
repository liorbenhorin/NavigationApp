﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Animations;
using CoordinateSharp;
using RTG;
using B83;
using TMPro;

public enum RenderOrientation
{
    Portrait,
    Landscape
}

struct FlightLegData
{
    public GameObject leg;
    public GameObject start;
    public GameObject end;
    public Leg script;
}

[System.Serializable]
public class RATA_waypoint
{
    public string name;
    public float[] coord;
}

[System.Serializable]
public class RATA_waypoints
{
    public RATA_waypoint[] waypoints;
}

[System.Serializable]
public class RATA_waypoint_go
{
    public RATA_waypoint waypoint_data;
    public GameObject waypoints_go;
}


public class Main : MonoBehaviour
{

    //public GameObject crosshair;
    public GameObject waypoint;
    public GameObject leg;
    public GameObject mainCamera;
    public GameObject buttonDraw;
    public float dragSpeed = 15;
    public float camZoomStep = 0.5F;
    public Tut_1_Enabling_and_Disabling_Gizmos gizmos;
    public Texture2D cursorTextureDrag;
    public Texture2D cursorTextureDragAction;
    public CursorMode cursorMode = CursorMode.Auto;
    public Vector2 hotSpot = Vector2.zero;
    public DataIO dataIO;
    public GameObject editorCanvas;
    public GameObject renderCanvas;
    public RenderOrientation renderOrientation = RenderOrientation.Portrait;
    // public Vector2 renderAspectRatio = new Vector2(1,1.42F);
    public Canvas canvas;
    public GameObject mapGO;

    public Inspector inspector;

    public AspectRatioFitter safeAspectRatio;

    public GameObject exportCamera;
    public double flightSpeed = 90d; //TODO: Will be on the leg level
    public Toolbar toolbar;

    public GameObject print_frames_rot;
    public Tool A3buttonTool;
    public Tool A4buttonTool;
    public bool showAccumulatedFlightTime = true;
    public bool showShowReturnLeg = true;

    public GameObject rata_waypoint_go;

    private List<FlightLegData> flight = new List<FlightLegData>();
    private List<GameObject> waypoints = new List<GameObject>();
    private Vector3 lastMouse;
    private Transform mapCameraTransform;
    private Camera mapCameraCamera;
    private Transform exportCameraTransform;
    private Camera exportCameraCamera;
    private float zoom = 15;
    private bool canZoom;
    private bool snapshot;
    // private LineRenderer renderSafeFrameLine;
    // private Vector2 renderCurAspectRatio;
    // private int renderCurHeight;

    // static varibales to define the world scale
    private static double lonConversionRate = 8.392355; //of NM=0.16666667 degrees along Longatiute
    private static double latConversionRate = 10.00674; //of NM=0.16666667 degrees along Latitude
    private static double NMConversionRate = 0.1666667;
    private static double lonOriginRadians = 35d;
    private static double latOriginRadians = 33d;
    private bool resumeDrawing;
    
    private List<RATA_waypoint_go> rata_waypoints_go = new List<RATA_waypoint_go>();

    // Start is called before the first frame update
    private void Start()
    {
        // TODO: is this in fact the correct way to optimize for WebGL?
        Application.targetFrameRate = 24;
        safeAspectRatio.aspectRatio = 0.704F; //portrait
        renderCanvas.GetComponent<AspectRatioFitter>().aspectRatio = 0.704F;

        

        mapCameraTransform = mainCamera.transform;
        mapCameraCamera = mainCamera.GetComponent<Camera>();
        exportCamera.SetActive(false);
        mapCameraCamera.orthographicSize = zoom;

        exportCameraTransform = exportCamera.transform;
        exportCameraCamera = exportCamera.GetComponent<Camera>();

        renderCanvas.SetActive(false);

        toolbar.eventDrawing.AddListener(SetStartDrawing);
        toolbar.eventStopDrawing.AddListener(SetStopDrawing);
        toolbar.eventActionTriggered.AddListener(DoAction);

        // renderSafeFrameLine = renderSafeFrame.GetComponent<LineRenderer>();
        // renderCurAspectRatio = renderAspectRatio;
        // DrawRenderSafe();

        load_RATA_waypoints();
    }

    private void update_rata_points()
    {
        Camera sceneCamera = mainCamera.GetComponent<Camera>();

        foreach(RATA_waypoint_go waypoint in rata_waypoints_go)
        {
            Vector3 scenePosition = CoordinateToScene(waypoint.waypoint_data.coord[1], waypoint.waypoint_data.coord[0]);
            RectTransform uiRectTransform = waypoint.waypoints_go.GetComponent<RectTransform>();
            Vector3 screenPosition = sceneCamera.WorldToScreenPoint(scenePosition);
            Vector2 finalPosition = new Vector2(screenPosition.x, screenPosition.y);
            uiRectTransform.anchoredPosition  = finalPosition;

            TMPro.TextMeshProUGUI text = waypoint.waypoints_go.GetComponent<TextMeshProUGUI>();
            text.enabled = sceneCamera.orthographicSize < 24;
        }

    }


    private void load_RATA_waypoints()
    {
        TextAsset waypointsFile = Resources.Load<TextAsset>("user-waypoints");
        var waypointData = JsonUtility.FromJson<RATA_waypoints>(waypointsFile.text);

        foreach (RATA_waypoint waypoint in waypointData.waypoints)
        {
            Debug.Log($"Waypoint Name: {waypoint.name}");
            Debug.Log($"Latitude: {waypoint.coord[0]}");
            Debug.Log($"Longitude: {waypoint.coord[1]}");

            GameObject wp = Instantiate(rata_waypoint_go, transform.position, transform.rotation) as GameObject;
            wp.name = $"wp_{waypoint.name}";
            TMPro.TextMeshProUGUI text = wp.GetComponent<TextMeshProUGUI>();
            text.text = waypoint.name;
            wp.transform.parent = canvas.transform;
            
            RATA_waypoint_go w = new RATA_waypoint_go();
            w.waypoints_go = wp;
            w.waypoint_data = waypoint;
            rata_waypoints_go.Add(w);
        }
    }

    public static int ToMagnetic(int angle = 360, int divation = -5)
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

    public static double ToSeconds(double time)
    {
        var result = TimeSpan.FromHours(time);
        return result.TotalSeconds;
    }


    public static string ToHMSFromSeconds(double seconds)
    {
        var result = TimeSpan.FromSeconds(seconds);
        int sec = (int)Math.Round(result.Seconds / 5.0) * 5;
        return result.Minutes + ":" + sec.ToString("D2"); 
        // return result.ToString(@"mm\:ss");
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

    public static Vector3 CoordinateToScene(double latitudeDecimalDegrees, double longitudeDecimalDegrees)
    {
        // x --> lon (presented as NM dist from 35E lon)
        // z --> lat (presented as NM dist from 33N lat)

        double lat = (latitudeDecimalDegrees - latOriginRadians) / NMConversionRate * latConversionRate;
        double lon = (longitudeDecimalDegrees - lonOriginRadians) / NMConversionRate * lonConversionRate;

        return new Vector3((float)lon, 0f, (float)lat);
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

    // private void DrawRenderSafe()
    // {
        
    //     var screen_top = Camera.main.ScreenToWorldPoint(new Vector2(0,2));
    //     var screen_bottom = Camera.main.ScreenToWorldPoint(new Vector2(0,Screen.height - 2));
    //     var height = screen_bottom.z - screen_top.z;
    //     float ratio = renderAspectRatio.x / renderAspectRatio.y;

    //     Vector2 frameSize = new Vector2(ratio*height, height);
    //     renderSafeFrameLine.SetPosition(0, new Vector3(frameSize.x/2, 0, frameSize.y/2)); //top-right
    //     renderSafeFrameLine.SetPosition(1, new Vector3(frameSize.x/2, 0, -frameSize.y/2)); //bottom-right
    //     renderSafeFrameLine.SetPosition(2, new Vector3(-frameSize.x/2, 0, -frameSize.y/2));//bottom-left
    //     renderSafeFrameLine.SetPosition(3, new Vector3(-frameSize.x/2, 0, frameSize.y/2));//top-left
    //     renderSafeFrameLine.SetPosition(4, new Vector3(frameSize.x/2, 0, frameSize.y/2));//top-right(start)
    // }

    private void SetStopDrawing()
    {
        if (WaypointsExists())
        {
            RemoveLastWaypoint();
            gizmos.OnTargetObjectChanged(null);
            UpdateAccumalatedTimes();
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
        wp.GetComponent<Waypoint>().mainLoop = this;
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
                Update();
                break; 

            case ToolType.Settings:
                ShowMapSettings();
                break; 

            case ToolType.A3_Zoom:
                print("Zoom to A3");
                Camera_Zoom("A3");
                break; 
            case ToolType.A4_Zoom:
                print("Zoom to A4");
                Camera_Zoom("A4");
                break; 
        }
    }

    private int GetRenderOrientationIndex()
    {
        int index = (int)renderOrientation;
        print(index);
        return index;
    }

    public void SetRenderOrientation(int orienation)
    {
        renderOrientation = (RenderOrientation)orienation;
        switch(renderOrientation)
        {
            case RenderOrientation.Portrait:
                safeAspectRatio.aspectRatio = 0.707F;
                renderCanvas.GetComponent<AspectRatioFitter>().aspectRatio = 0.707F;
                print("portrait");
                print_frames_rot.transform.eulerAngles = new Vector3(0,0,0);
                break;
            case RenderOrientation.Landscape:
                safeAspectRatio.aspectRatio = 1.414F;
                renderCanvas.GetComponent<AspectRatioFitter>().aspectRatio = 1.414F;
                print("lendsacpe");
                print_frames_rot.transform.eulerAngles = new Vector3(0,90,0);
                break;
        }

        A4buttonTool.SetUnSelected();
        A3buttonTool.SetUnSelected();
    }

    public void SetGlobalSpeed(string speed)
    {
        flightSpeed = (double)Convert.ToUInt32(speed);
        for (int i = 0; i<flight.Count; i++)
        {
            flight[i].script.flightSpeed = flightSpeed;
        }
        Camera.main.Render();
    }


    public int GetGlobalSpeed()
    {
        if(flight.Count==0){return 90;}
        
        List<int> speed = new List<int>();
        for (int i = 0; i<flight.Count; i++)
        {
            speed.Add((int)flight[i].script.flightSpeed);
        }
        List<int> noDups = speed.Distinct().ToList();
        if (noDups.Count > 1) return 0; // the is more then one speed in the map

        return noDups[0]; //only one speed in the map   

    }

    public void SetMapOpacity(float opacity)
    {
        print(opacity);
        Transform offset_root = mapGO.transform.GetChild(0);

        int tilesCount = offset_root.childCount;
        for (int i = 0; i < tilesCount; i++)
        {
            Transform childTransform = offset_root.GetChild(i);
            Material mat = childTransform.GetComponent<Renderer>().material;
        
            var c = new Color(mat.color.r, mat.color.g, mat.color.b, opacity);
            mat.color = c;
            
        }
        // var map_tiles = mapGO.GetComponent<Renderer>().material;


        // var c = new Color(m1.color.r, m1.color.g,m1.color.b, opacity);

        // m1.color = c;
        // m2.color = c;
        // m3.color = c;
        // m4.color = c;

    }

    public float GetMapOpacity()
    {
        // var m = mapGO.GetComponent<Renderer>().material;
        // return m.color.a;
        return 1f;
    }


    public void ToggleAccumulatedFlightTime(bool state)
    {
        showAccumulatedFlightTime = state;
        UpdateAccumalatedTimes();
    }

    public void ToggleShowReturnLeg(bool state)
    {
        showShowReturnLeg = state;
        UpdateShowBidirectional();
    }

    private void Camera_Zoom(string size)
    {

        mapCameraTransform = mainCamera.transform;
        mapCameraCamera = mainCamera.GetComponent<Camera>();

        int orienation = GetRenderOrientationIndex();
        renderOrientation = (RenderOrientation)orienation;
        

        switch(size)
        {
            case "A3":
                switch(renderOrientation)
                {
                    case RenderOrientation.Portrait:
                        zoom = 29;
                        break;  
                    case RenderOrientation.Landscape:
                        zoom = 20.5F;
                        break;    
                } 
                A3buttonTool.SetSelected(); 
                A4buttonTool.SetUnSelected(); 
                break;
            case "A4":
                switch(renderOrientation)
                {
                    case RenderOrientation.Portrait:
                        zoom = 20.5F;
                        break;  
                    case RenderOrientation.Landscape:
                        zoom = 14.5F;
                        break;    
                }
                A4buttonTool.SetSelected();
                A3buttonTool.SetUnSelected();      
                break;
        }

        mapCameraCamera.orthographicSize = zoom;
        canZoom = false;
    }


    private void ShowMapSettings()
    {
        List<Param> paramaters = new List<Param>();

        Param _renderOrientation = new Param();
        _renderOrientation.type = ParamType.Enum;
        _renderOrientation.name = "Render Orientation";
        _renderOrientation.intCallback = new UnityAction<int>(SetRenderOrientation);
        List<string> RenderOrientationValues = ((RenderOrientation[])Enum.GetValues(typeof(RenderOrientation))).Select(c => c.ToString()).ToList();
        _renderOrientation.enumOptions = RenderOrientationValues;
        _renderOrientation.intInitialValue = GetRenderOrientationIndex();


        Param mapOpacity = new Param();
        mapOpacity.type = ParamType.Slider;
        mapOpacity.name = "Map opacity";
        mapOpacity.floatCallback = new UnityAction<float>(SetMapOpacity);
        mapOpacity.floatInitialValue = GetMapOpacity();


        Param speed = new Param();
        speed.type = ParamType.Standard;
        speed.name = "Speed (Global)";
        speed.callback = new UnityAction<string>(SetGlobalSpeed);
        var globalSpeed = GetGlobalSpeed();
        if (globalSpeed==0)
        {
            speed.intialValue = "variable";
        } 
        else
        {
            speed.intialValue = globalSpeed.ToString();    
        }

        Param accumulatedTime = new Param();
        accumulatedTime.type = ParamType.Bool;
        accumulatedTime.name = "Show Accumulated flight time";
        accumulatedTime.boolCallback = new UnityAction<bool>(ToggleAccumulatedFlightTime);
        accumulatedTime.intialBoolValue = true;

        Param showReturnLeg = new Param();
        showReturnLeg.type = ParamType.Bool;
        showReturnLeg.name = "Show Return Leg";
        showReturnLeg.boolCallback = new UnityAction<bool>(ToggleShowReturnLeg);
        showReturnLeg.intialBoolValue = true;

        paramaters.Add(_renderOrientation);
        paramaters.Add(speed);
        paramaters.Add(mapOpacity);
        paramaters.Add(accumulatedTime);
        paramaters.Add(showReturnLeg);
        inspector.Edit(this.gameObject, "Map settings", paramaters);   
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


    public void UpdateAccumalatedTimes()
    {  
        if (flight.Count>0)
        {
            double accTime = 0d;
            for (int i=0; i<flight.Count; i++)
            {
                accTime += flight[i].script.TimeInSceonds;
                flight[i].script.AccumulatedTimeFromStart = accTime;
                
            }

            for (int i=0; i<flight.Count; i++)
            //for (int i=flight.Count-1; i >= 0; i--)
            {
                flight[i].script.AccumulatedTimeFromEnd = accTime;
                accTime -= flight[i].script.TimeInSceonds;
                flight[i].script.updateAccumlatedTimes();
            }
            
            //print(accTime);
        }
    }

    public void UpdateShowBidirectional()
    {  
        if (flight.Count>0)
        {
            for (int i=0; i<flight.Count; i++)
            {
                flight[i].script.updateShowBidirectional();
            }
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
                    l.script.mainLoop = this;
                    l.script.flightSpeed = flightSpeed;
                    l.script.inspector = inspector;
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

        UpdateAccumalatedTimes(); 

    }

    // Update is called once per frame
    void Update()
    {
        
        // if (renderAspectRatio != renderCurAspectRatio){
        //     renderCurAspectRatio = renderAspectRatio;
        //     DrawRenderSafe();
        // }
        
        //if (!Input.GetKey(KeyCode.Space)) Cursor.SetCursor(null, Vector2.zero, cursorMode);    
        
        var objectPos = CursorLocalPosition();
        // TODO: edit mode, delete mode

        //crosshair.transform.position = objectPos;

        
        if (toolbar.currentTool != ToolType.None && (Input.GetKeyDown(KeyCode.Escape) | Input.GetKeyDown(KeyCode.Return)))
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
            // print(zoom);
            mapCameraCamera.orthographicSize = zoom;
            A4buttonTool.SetUnSelected();
            A3buttonTool.SetUnSelected();
            // DrawRenderSafe();
            update_rata_points();
        }


        // if (Input.GetKey(KeyCode.Space))
        // {
            //Cursor.SetCursor(cursorTextureDrag, hotSpot, cursorMode); 
        if (toolbar.currentTool == ToolType.None && !gizmos.is_alive)
        {   

            // filter UI drags
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            System.Collections.Generic.List<RaycastResult> results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            if (results.Count > 0){
                return;
            }
       

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
                update_rata_points();
                return;
                }
            // }
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
                        Waypoint wp = o.GetComponent<Waypoint>();
                        if (wp)
                        {
                            waypoints.Remove(o);
                            gizmos.OnTargetObjectChanged(null);
                            Destroy(o);
                            DrawLegs();
                        }
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
            //Update();    
            print_frames_rot.SetActive(false);
            editorCanvas.SetActive(false);
            renderCanvas.SetActive(true);
            exportCamera.SetActive(true);
            exportCameraCamera.orthographicSize = mapCameraCamera.orthographicSize;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = exportCameraCamera;
            canvas.planeDistance = 1;
            

            

            int resWidth = 1;
            int resHeight = 1;
            switch (renderOrientation)
            {
                case RenderOrientation.Portrait:
                    resWidth = 3508;
                    resHeight = 4960; // 4K A3 proportions, will be fine for print
                    break;
                case RenderOrientation.Landscape:
                    resWidth = 4960;
                    resHeight = 3508; // 4K A3 proportions, will be fine for print
                    break;
            }
            
            RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
            exportCameraCamera.targetTexture = rt;
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            exportCameraCamera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            exportCameraCamera.targetTexture = null;
            RenderTexture.active = null; // JC: added to avoid errors
            Destroy(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            byte[] ppi_bytes = B83.Image.PNGTools.ChangePPI(bytes, 300F, 300F); // upres to 300dpi

            dataIO.DownloadPrint(ppi_bytes);
            snapshot = false;
            exportCamera.SetActive(false);

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;

            renderCanvas.SetActive(false);
            editorCanvas.SetActive(true);
            print_frames_rot.SetActive(true);
        }
    }

}


