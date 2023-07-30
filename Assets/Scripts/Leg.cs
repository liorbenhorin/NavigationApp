using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CoordinateSharp;
using UnityEngine.Animations;


public struct MinuteMarker
{
    public GameObject Marker;
    public GameObject Text;

}

public struct MinutePair
{
    public MinuteMarker Inbound;
    public MinuteMarker Outbound;

}

//[ExecuteInEditMode]
public class Leg : MonoBehaviour
{
    
    public GameObject start;
    public GameObject end;
    public Main mainLoop;
    public GameObject startSource = null;
    public GameObject endSource = null;
    public Waypoint startWaypoint = null;
    public Waypoint endWaypoint = null;
    public double flightSpeed = 90d; 
    public int inboundAltitude = 2000;
    public int outboundAltitude = 2000;

    public double TimeInSceonds = 0;
    public double AccumulatedTimeFromStart = 0;
    public double AccumulatedTimeFromEnd = 0;

    public GameObject legInfo;
    public GameObject midLegGroup;
    public GameObject emptyLine;
    public GameObject minuteText;
    public TextMesh distanceText;
    public TextMesh AccEndTimeText;
    public TextMesh AccStartTimeText;
    public GameObject selectionArea;
    public bool drawMidLegIndication = true;
    public bool dirty;

    public Inspector inspector;

    //public Coordinate startCoord;
    //public Coordinate endCoord;
    private double curSpeed;
    private int curInboundAltitude = 2000;
    private int curOutboundAltitude = 2000;
    private LineRenderer lineRenderer;
    //private LineRenderer middleLineRenderer;
    private Vector3 lastStart;
    private Vector3 lastEnd;
    private GameObject inboundLegInfo;
    private GameObject outboundLegInfo;
    private GameObject inboundLegInfoTra;
    private GameObject outboundLegInfoTra;
    private GameObject inboundDriftGO;
    private GameObject outboundDriftGO;
    private LineRenderer inboundDrift;
    private LineRenderer outboundDrift;
    
    
    private List<MinutePair> minutes = new List<MinutePair>();
    
    
    // Start is called before the first frame update
    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;

        curSpeed = flightSpeed;
        midLegGroup.SetActive(drawMidLegIndication);
        //flightSpeed = (double)(startWaypoint.speed);

        //var middleGameObject = PerpendicularLine();
        //middleLineRenderer = initLine(middleGameObject);

        lastStart = start.transform.position;
        lastEnd = end.transform.position;

        inboundLegInfo = Instantiate(legInfo, Vector3.zero, Quaternion.identity);
        inboundLegInfo.transform.SetParent(gameObject.transform);
        inboundLegInfo.SetActive(true);
        outboundLegInfo = Instantiate(legInfo, Vector3.zero, Quaternion.identity);
        outboundLegInfo.transform.SetParent(gameObject.transform);
        outboundLegInfo.GetComponent<RotationConstraint>().rotationOffset = new Vector3(0, 0, 0);
        // outboundLegInfo.SetActive(true);

        inboundLegInfoTra = inboundLegInfo.transform.GetChild(0).gameObject;
        outboundLegInfoTra = outboundLegInfo.transform.GetChild(0).gameObject;

        Material dashedMat = Resources.Load("dashedReadStencil", typeof(Material)) as Material;

        inboundDriftGO = Instantiate(emptyLine, Vector3.zero, Quaternion.identity);
        inboundDriftGO.transform.SetParent(gameObject.transform);
        inboundDriftGO.GetComponent<Renderer>().material = dashedMat;
        inboundDrift = inboundDriftGO.GetComponent<LineRenderer>();
        inboundDrift.positionCount = 2;
        inboundDrift.startWidth = 0.05f;
        inboundDrift.endWidth = 0.05f;

        outboundDriftGO = Instantiate(emptyLine, Vector3.zero, Quaternion.identity);
        outboundDriftGO.transform.SetParent(gameObject.transform);
        outboundDriftGO.GetComponent<Renderer>().material = dashedMat;
        outboundDrift = outboundDriftGO.GetComponent<LineRenderer>();
        outboundDrift.positionCount = 2;
        outboundDrift.startWidth = 0.05f;
        outboundDrift.endWidth = 0.05f;


    }

    private GameObject PerpendicularLine()
    {
        var go = Instantiate(emptyLine, Vector3.zero, Quaternion.identity);
        go.transform.SetParent(gameObject.transform);
        return go;
    }

    private LineRenderer initLine(GameObject go)
    {
        LineRenderer result = go.GetComponent<LineRenderer>();
        result.startWidth = 0.1f;
        result.endWidth = 0.1f;
        return result;
    }
    
    
    private List<Vector3> GetPerpendicularPoints(Vector3 startPosition, Vector3 endPosition, float cutPos)
    {
        var newVec = startPosition - endPosition;
        var newVector = Vector3.Cross(newVec, Vector3.up);
        newVector.Normalize();

        var newPoint = 1 * newVector + ((startPosition + endPosition) / cutPos);
        var newPoint2 = -1 * newVector + ((startPosition + endPosition) / cutPos);

        List<Vector3> result = new List<Vector3> {newPoint, newPoint2};

        return result;
    }

    private bool IsEven(int number)
    {
        if(number%2 == 0)
        {
            return true;
        }else
        {
            return false;
        }
    }
    
    public void updateAccumlatedTimes()
    {
        
        if (mainLoop.showAccumulatedFlightTime == false)
        {
            AccEndTimeText.gameObject.transform.parent.gameObject.SetActive(false); 
            AccStartTimeText.gameObject.transform.parent.gameObject.SetActive(false); 
        }
        else
        {
            AccEndTimeText.gameObject.transform.parent.gameObject.SetActive(true);
            AccStartTimeText.gameObject.transform.parent.gameObject.SetActive(false); 
            AccEndTimeText.text = Main.ToHMSFromSeconds(AccumulatedTimeFromStart);//AccumulatedTimeFromStart.ToString("n1");
            AccStartTimeText.text = Main.ToHMSFromSeconds(AccumulatedTimeFromEnd);
        }
    }

    public void showAccumlatedTimes(bool state)
    {
        AccEndTimeText.gameObject.SetActive(false);
        AccStartTimeText.gameObject.SetActive(state);
    }

    // Update is called once per frame
    void Update()
    {

        if (startSource == null || endSource == null) return;
        
        //flightSpeed = (double)(startWaypoint.speed);


        // if (startSource != null)
        // {
        start.transform.position = startSource.transform.position;
        // }

        // if (endSource != null)
        // {
        end.transform.position = endSource.transform.position;
        // }

                
        var startPosition = start.transform.position;
        var endPosition = end.transform.position;

        midLegGroup.SetActive(drawMidLegIndication);
    
        
        //waypoint/speed/altitude changed
        if ((startPosition != lastStart || endPosition != lastEnd)
            || (curSpeed != flightSpeed)
            || (curInboundAltitude != inboundAltitude)
            || (curOutboundAltitude != outboundAltitude)
            || dirty)
        {

            dirty = false;    
            // zero out this as the last position
            lastStart = startPosition;
            lastEnd = endPosition;
            curSpeed = flightSpeed;
            curInboundAltitude = inboundAltitude;
            // curOutboundAltitude = outboundAltitude;

            // update everything...


            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, endPosition);


            var legDistance = new Distance(startWaypoint.coordinate, endWaypoint.coordinate, Shape.Sphere);
            var legDistanceNm = legDistance.NauticalMiles;

            distanceText.text = legDistanceNm.ToString("n1");

            

            selectionArea.transform.localScale = new Vector3(0.2F, 1, (float)legDistanceNm/10);

            // draw Leg info arrows
            var newVec = startPosition - endPosition;
            var newVector = Vector3.Cross(newVec, Vector3.up);
            newVector.Normalize();


            var bearing = Main.ToMagnetic(Convert.ToInt32(legDistance.Bearing));
            var inverseBearing = (bearing + 180) % 360;
            var duration = legDistanceNm / flightSpeed;
            var durationMinutes = Main.ToHMS(duration);
            TimeInSceonds = Main.ToSeconds(duration);

            

            var inboundLegInfoHeading = inboundLegInfoTra.transform.GetChild(0).gameObject;
            inboundLegInfoHeading.GetComponent<TextMesh>().text = bearing.ToString("D3");
            var inboundLegInfoDuration = inboundLegInfoTra.transform.GetChild(1).gameObject; //TODO: how to I get the childs in a nicer fashion?
            inboundLegInfoDuration.GetComponent<TextMesh>().text = durationMinutes; //rounded to nearest 5 seconds
            inboundLegInfo.transform.position =  (3f * newVector) + ((startPosition + endPosition) / 2f);
            var inboundLegInfoAltitude = inboundLegInfoTra.transform.GetChild(2).gameObject;
            inboundLegInfoAltitude.GetComponent<TextMesh>().text = inboundAltitude.ToString();

            // var outboundLegInfoHeading = outboundLegInfoTra.transform.GetChild(0).gameObject;
            // outboundLegInfoHeading.GetComponent<TextMesh>().text = inverseBearing.ToString("D3");
            // var outboundLegInfoDuration = outboundLegInfoTra.transform.GetChild(1).gameObject;
            // outboundLegInfoDuration.GetComponent<TextMesh>().text = durationMinutes;
            // outboundLegInfo.transform.position = (-3f * newVector) + ((startPosition + endPosition) / 2f);
            // var outboundLegInfoAltitude = outboundLegInfoTra.transform.GetChild(2).gameObject;
            // outboundLegInfoAltitude.GetComponent<TextMesh>().text = outboundAltitude.ToString();

            // var outboundLegInfoBackground = outboundLegInfoTra.transform.GetChild(3).gameObject;
            // outboundLegInfoBackground.GetComponent<MeshRenderer>().material.color = new Color(1,0,0,0.33f);

            var inboundLegInfoBackground = inboundLegInfoTra.transform.GetChild(3).gameObject;
            inboundLegInfoBackground.GetComponent<MeshRenderer>().material.color = new Color(1, 1, 0, 0.25f);

            int legTimeMinutes = TimeSpan.FromHours((legDistanceNm /  flightSpeed)).Minutes;
            
            //TODO: this is sort of constant by the flightSpeed...
            float minuteLength = (float)legDistanceNm / ((float)(legDistanceNm /  flightSpeed)*60f);
            
            //print("BEFORE: minutes.Count: " +  minutes.Count + " leg minutes: " + roundedMinutes);
            
            // clean up redundant minute markers (when the leg shortens...)
            int ii=minutes.Count;
            while (minutes.Count > 0 &&  minutes.Count > legTimeMinutes && ((ii - 1) < minutes.Count))
            {
                Destroy(minutes[ii-1].Inbound.Marker);
                // Destroy(minutes[ii-1].Outbound.Marker);
                minutes.RemoveAt(ii-1);
                ii++;
            }
            

            if (legTimeMinutes > 0)
            {
                //Add missing minute markers
                for (int i = minutes.Count; i <= legTimeMinutes-1; i+=1)
                {

                    MinutePair pair = new MinutePair();
                    
                    MinuteMarker inbound = new MinuteMarker();
                    MinuteMarker outbound = new MinuteMarker();
                    
                    inbound.Marker = Instantiate(emptyLine, new Vector3(0,-0.1f,0), Quaternion.identity);
                    inbound.Marker.transform.SetParent(gameObject.transform);
                    inbound.Marker.GetComponent<Renderer>().material.color = Color.blue;
                    
                    // outbound.Marker = Instantiate(emptyLine, new Vector3(0,-0.1f,0), Quaternion.identity);
                    // outbound.Marker.transform.SetParent(gameObject.transform);
                    // outbound.Marker.GetComponent<Renderer>().material.color = Color.red;
                    
                    // TODO: Need to do this only for even minutes!

                    if (!IsEven(i))
                    {
                        inbound.Text = Instantiate(minuteText, Vector3.zero, Quaternion.identity);
                        inbound.Text.transform.SetParent(inbound.Marker.transform);
                        inbound.Text.GetComponent<Renderer>().material.color = Color.blue;

                        // outbound.Text = Instantiate(minuteText, Vector3.zero, Quaternion.identity);
                        // outbound.Text.transform.SetParent(inbound.Marker.transform);
                        // outbound.Text.GetComponent<Renderer>().material.color = Color.red;
                        //flip the label for the outbound markers
                        // outbound.Text.GetComponent<RotationConstraint>().rotationOffset = new Vector3(90f, 0, 0);
                    }

                    pair.Inbound = inbound;  
                    // pair.Outbound = outbound;  
                    
                    minutes.Add(pair);
                    //print("added minute: " + i);
                }
                
                // Update the current markers
                for (int i = 0; i <= legTimeMinutes-1; i+=1)
                {
                    var inboundMarkerLineRenderer = minutes[i].Inbound.Marker.GetComponent<LineRenderer>();
                    // var outboundMarkerLineRenderer = minutes[i].Outbound.Marker.GetComponent<LineRenderer>();

                    
                    // gets the minute marker position along the leg
                    var inboundMarkerPosition = (minuteLength * (i+1)) * Vector3.Normalize(endPosition - startPosition) +
                                         startPosition;
                    
                    // var outboundMarkerPosition = (minuteLength * (i+1)) * Vector3.Normalize(startPosition - endPosition) +
                    //                             endPosition;
                    
                    // get perpendicular vector of this leg
                    //var newVec = startPosition - endPosition;
                    //var newVector = Vector3.Cross(newVec, Vector3.up);
                    //newVector.Normalize();

                    // for even minutes let's make them longer, and with a text
                    float markerLength = 0.5f;
                    if (IsEven(i + 1))
                    {
                        markerLength = 1;
                        var inboundText = minutes[i].Inbound.Text;
                        inboundText.transform.position = inboundMarkerPosition + ((markerLength+0.5f) * newVector);
                        inboundText.SetActive(true);
                        inboundText.GetComponent<TextMesh>().text = (i + 1).ToString();
                        
                        // var outboundText = minutes[i].Outbound.Text;
                        // outboundText.transform.position = outboundMarkerPosition + ((-markerLength-0.5f) * newVector);
                        // outboundText.SetActive(true);
                        // outboundText.GetComponent<TextMesh>().text = (i + 1).ToString();
                    }

                    // draw the two points of the minute marker
                    inboundMarkerLineRenderer.SetPosition(0, inboundMarkerPosition + (markerLength * newVector));
                    inboundMarkerLineRenderer.SetPosition(1, inboundMarkerPosition);
                    
                    // outboundMarkerLineRenderer.SetPosition(0, outboundMarkerPosition + (-markerLength * newVector));
                    // outboundMarkerLineRenderer.SetPosition(1, outboundMarkerPosition);
                    
                }
            }


            // drift lines
            
            
            var rot10deg = Quaternion.Euler(0, 10f, 0);

            var rot10degPivot = rot10deg * (endPosition - startPosition);
            var rotatedEndPosition = rot10degPivot + startPosition;
            Vector3 angledVector = (startPosition + rotatedEndPosition) / 2;
            inboundDrift.SetPosition(0, startPosition);
            inboundDrift.SetPosition(1, angledVector);

            // var rotNeg10degPivot = rot10deg * (startPosition - endPosition);
            // var rotatedStartPosition = rotNeg10degPivot + endPosition;
            // Vector3 angledNegVector = (endPosition + rotatedStartPosition) / 2;
            // outboundDrift.SetPosition(0, endPosition);
            // outboundDrift.SetPosition(1, angledNegVector);

            inboundDriftGO.GetComponent<Renderer>().material.mainTextureScale = new Vector2((float)legDistanceNm, 1);
            // outboundDriftGO.GetComponent<Renderer>().material.mainTextureScale = new Vector2((float)legDistanceNm, 1);
        }

    }

}
