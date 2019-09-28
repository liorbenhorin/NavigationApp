﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CoordinateSharp;


public class MainLoop : MonoBehaviour
{

    public GameObject leg;
    public GameObject mainCamera;
    public GameObject buttonDraw;
    public float dragSpeed = 20;
    public int camZoomStep = 5;


    // Start is called before the first frame update
    private List<FlightLeg> flight = new List<FlightLeg>();
    private Vector3 lastMouse;
    private Transform map_camera_transform;
    private Camera map_camera_camera;
    private float zoom;
    private bool canZoom;
    private bool drawMode;

    // static varibales to define the world scale
    private double lonConversionRate = 8.392355; //of NM=0.16666667 degrees along Longatiute
    private double latConversionRate = 10.00674; //of NM=0.16666667 degrees along Latitude
    private double NMConversionRate = 0.1666667;
    private double lonOriginRadians = 35d;
    private double latOriginRadians = 33d;

    private bool resumeDrawing;

    public double flightSpeed = 90d; //TODO: Will be on the leg level



    void Start()
    {
        map_camera_transform = mainCamera.transform;
        map_camera_camera = mainCamera.GetComponent<Camera>();
        buttonDraw.GetComponent<Button>().onClick.AddListener(ToggleDrawMode);
    }


    public int ToMagnetic(int angle = 360, int divation = -4)
    {
        int result = angle + divation;
        if (result < 0)
        {
            result += 360;
        }
        return result;
    }

    public string ToHMS(double time)
    {
        var result = TimeSpan.FromHours(time);
        return result.Minutes + "' " + result.Seconds + "''"; //result.Hours + ": " +  Only return minutes seconds
    }

    public Coordinate CursorToCoordinate(Vector3 cursorPosition)
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

        /* more precise method:
        
        Vector3 origin = cursorPosition;
        origin.x = 0;
        origin.z = 0;

        //print("x: " + objectPos.x + " y: " + objectPos.y + " z: " + objectPos.z);

        // get angle from origin to cursor point
        float angle = Mathf.Atan2(objectPos.x - origin.x, objectPos.z - origin.z) * Mathf.Rad2Deg;
        if (angle < 0) angle = 1 * angle + 360;

        // convert the dist in NM to Meters
        float dist_as_NM = Vector3.Distance(origin, objectPos);
        float dist_as_Meteres = dist_as_NM * 1852;//new Distance(dist_as_NM, DistanceType.NauticalMiles).Meters;

        //print("Caculate using: distance(NM): " + dist_as_NM + " distance(Meters): " + dist_as_Meteres + " heading: " + angle);

        Coordinate c = new Coordinate(33.0, 35.0);
        c.Move(dist_as_Meteres, angle, Shape.Sphere);

        //print("Pressed at: " + c);
        return null;
        */

    }

    public Vector3 CursorLocalPosition()
    {
        var mousePos = Input.mousePosition;
        mousePos.z = 5f;       // we want 2m away from the camera position
        return Camera.main.ScreenToWorldPoint(mousePos);
    }


    private void ToggleDrawMode()
    {
        drawMode = !drawMode;
        if (!drawMode) FinishDraw();
        if (drawMode && FlightHasLegs()) resumeDrawing = true;
    }


    private void FinishDraw()
    {
        Debug.Log("Finish Drawing!");
        if (FlightHasLegs()) RemoveLastLeg();
    }


    private bool FlightHasLegs()
    {
        if (flight.Count > 0) return true;
        return false;
    }


    private void CreateLegAtCurrentPos()
    {
        var legName = "LEG" + (flight.Count + 1);
        FlightLeg newLeg = new FlightLeg(legName);
        newLeg.CreateAtCurrentPos(this);
        flight.Add(newLeg);
    }

    //TODO: Duplicate code for just one line...
    private void CreateLegAtPos(Vector3 pos)
    {
        var legName = "LEG" + (flight.Count + 1);
        FlightLeg newLeg = new FlightLeg(legName);
        newLeg.CreateAtPos(this, pos);
        flight.Add(newLeg);
    }

    private FlightLeg GetLastFlightLeg()
    {
        return flight[flight.Count-1];
    }


    private void RemoveLastLeg()
    {
        FlightLeg lastLeg = flight[flight.Count - 1];
        Destroy(lastLeg.Leg); //Destory the gameObject
        flight.RemoveAt(flight.Count - 1);
    }


    // Update is called once per frame
    void Update()
    {
        var objectPos = CursorLocalPosition();
        // TODO: edit mode, delete mode

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

            map_camera_camera.orthographicSize = zoom;
        }


        if (Input.GetKey(KeyCode.Space))
        {
            if (Input.GetMouseButtonDown(0))
            {

                lastMouse = Input.mousePosition;
                return;
            }

            if (Input.GetMouseButton(0))
            {
                var curMouse = Input.mousePosition;
                var pos = Camera.main.ScreenToViewportPoint(lastMouse - curMouse);
                var orthographicSize = map_camera_camera.orthographicSize;
                var move = new Vector3(pos.x * dragSpeed * (orthographicSize / 10), 0, pos.y * dragSpeed * (orthographicSize / 10));
                lastMouse = curMouse;
                map_camera_transform.Translate(move, Space.World);
                return;
            }
        }

        ////////////////////////////////////////////////////////////
        // End of Screen navigation logic //////////////////////////

        if (!drawMode) return;

        // Draw mode:
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject()) // mouse left was clicked
        {
            CreateLegAtCurrentPos();
        }
        else if (resumeDrawing)
        {
            CreateLegAtPos(GetLastFlightLeg().PointEnd);
            resumeDrawing = false;
        }


        // mouse in motion with the next waypoint...
        if (!FlightHasLegs()) return;
        GetLastFlightLeg().UpdateLegEndFromScreen(objectPos, this);

    }
}


public class FlightLeg
{
    public string LegName;
    public Coordinate Start;
    public Coordinate End;
    public GameObject Leg;
    public LineDraw LegScript;
    public Vector3 PointStart;
    public Vector3 PointEnd;

    public FlightLeg(string name)
    {
        LegName = name;
    }

    public void CreateAtCurrentPos(MainLoop mainLoop)
    {
        var curPosition = mainLoop.CursorLocalPosition();
        var curCoords = mainLoop.CursorToCoordinate(curPosition);

        PointStart = curPosition;
        PointEnd = curPosition;

        var newLeg = UnityEngine.Object.Instantiate(mainLoop.leg, Vector3.zero, Quaternion.identity);

        Leg = newLeg;
        LegScript = newLeg.GetComponent<LineDraw>();
        LegScript.start.position = curPosition;
        LegScript.end.position = curPosition;

        Start = curCoords;
        End = curCoords;

    }

    //TODO: duplicate code for just one line... 
    public void CreateAtPos(MainLoop mainLoop, Vector3 pos)
    {
        var curPosition = pos;
        var curCoords = mainLoop.CursorToCoordinate(curPosition);

        PointStart = curPosition;
        PointEnd = curPosition;

        var newLeg = UnityEngine.Object.Instantiate(mainLoop.leg, Vector3.zero, Quaternion.identity);

        Leg = newLeg;
        LegScript = newLeg.GetComponent<LineDraw>();
        LegScript.start.position = curPosition;
        LegScript.end.position = curPosition;

        Start = curCoords;
        End = curCoords;

    }


    public void UpdateLegEndFromScreen(Vector3 pos, MainLoop mainLoop)

    {

        LegScript.end.position = pos;
        PointEnd = pos;
        End = mainLoop.CursorToCoordinate(pos); //TODO: do I really need this like this?

        var legDistance = new Distance(Start, End, Shape.Sphere);
        var legDistanceNm = legDistance.NauticalMiles;

        LegScript.distanceText.text = legDistanceNm.ToString("n1");
        LegScript.durationText.text = mainLoop.ToHMS(legDistanceNm / mainLoop.flightSpeed); //NOT HMS
        LegScript.headingText.text = mainLoop.ToMagnetic(Convert.ToInt32(legDistance.Bearing)) + " M";

        Leg.GetComponent<LineRenderer>().enabled = true;
    }

}