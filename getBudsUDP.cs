using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
 * Modified by xlinka to support both earbuds, fallback mechanism, and maintain functionality if one earbud dies.
 * 
 * Author: Hyung-il Kim
 * M.S. Student, KAIST UVR Lab.
 */

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class getBudsUDP : MonoBehaviour {

    Thread receiveThreadLeft;
    Thread receiveThreadRight;

    UdpClient clientLeft;
    UdpClient clientRight;

    public string TARGET_IP = "127.0.0.1";
    public int TARGET_PORT = 6969;

    public int RECEIVING_PORT_LEFT = 12562;
    public int RECEIVING_PORT_RIGHT = 12563; // Separate port for the right earbud

    public Vector4 rotLeft = new Vector4(.0f, .0f, .0f, .0f);
    public Vector4 rotRight = new Vector4(.0f, .0f, .0f, .0f);
    public Vector4 rot = new Vector4(.0f, .0f, .0f, .0f); // Combined rotation used for smoothing and output

    private Vector4 lastRot = new Vector4(0f, 0f, 0f, 1f);
    public float smoothFactor = 5.0f; // Adjust this for more or less smoothing

    private bool isLeftConnected = true;
    private bool isRightConnected = true;

    void Start() {
        Debug.Log("UDPSendReceive: Starting");
        this.init();
    }

    void Update() {
        if (Input.GetKeyDown("space")) {
            Debug.Log("Space key pressed - resetting rotation");
        }

        // Smoothly interpolate rotation using Quaternion.Slerp
        Quaternion currentRotation = transform.rotation;
        Quaternion targetRotation = new Quaternion(rot.x, rot.y, rot.z, rot.w);
        transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, Time.deltaTime * smoothFactor);
    }

    void OnDestroy() {
        if (receiveThreadLeft != null) receiveThreadLeft.Abort();
        if (receiveThreadRight != null) receiveThreadRight.Abort();
        if (clientLeft != null) clientLeft.Close();
        if (clientRight != null) clientRight.Close();
        Debug.Log("UDPSendReceive: OnDestroy");
    }

    private void init() {
        receiveThreadLeft = new Thread(() => ReceiveData(RECEIVING_PORT_LEFT, "left"));
        receiveThreadLeft.IsBackground = true;
        receiveThreadLeft.Start();

        receiveThreadRight = new Thread(() => ReceiveData(RECEIVING_PORT_RIGHT, "right"));
        receiveThreadRight.IsBackground = true;
        receiveThreadRight.Start();
    }

    private void ReceiveData(int port, string earbud) {
        UdpClient client = new UdpClient(port);
        Debug.Log($"Listening at port {port} for {earbud} earbud...");

        while (true) {
            try {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                string receiveStr = Encoding.UTF8.GetString(data);

                // Parse received quaternion data
                string[] subs = receiveStr.Split(',');
                Vector4 newRot = new Vector4(
                    float.Parse(subs[0]),
                    float.Parse(subs[1]),
                    float.Parse(subs[2]),
                    float.Parse(subs[3])
                );

                // Check for quaternion consistency
                float dot = Vector4.Dot(lastRot, newRot);
                if (dot < 0.0f) {
                    // Negate quaternion if dot product is negative
                    newRot = new Vector4(-newRot.x, -newRot.y, -newRot.z, -newRot.w);
                }

                if (earbud == "left") {
                    rotLeft = newRot;
                    isLeftConnected = true; // Mark left earbud as connected
                } else if (earbud == "right") {
                    rotRight = newRot;
                    isRightConnected = true; // Mark right earbud as connected
                }

                lastRot = newRot;

                if (isLeftConnected && isRightConnected) {
                    rot = AverageQuaternions(rotLeft, rotRight);
                } else if (isLeftConnected) {
                    rot = rotLeft;
                } else if (isRightConnected) {
                    rot = rotRight;
                }

            } catch (Exception err) {
                Debug.LogError($"Error receiving data from {earbud} earbud: {err.Message}");
                
                if (earbud == "left") isLeftConnected = false;
                if (earbud == "right") isRightConnected = false;
            }
        }
    }

    private Vector4 AverageQuaternions(Vector4 q1, Vector4 q2) {
        Vector4 avgQuat = new Vector4(
            (q1.x + q2.x) * 0.5f,
            (q1.y + q2.y) * 0.5f,
            (q1.z + q2.z) * 0.5f,
            (q1.w + q2.w) * 0.5f
        );

        float magnitude = Mathf.Sqrt(avgQuat.x * avgQuat.x + avgQuat.y * avgQuat.y + avgQuat.z * avgQuat.z + avgQuat.w * avgQuat.w);
        avgQuat.x /= magnitude;
        avgQuat.y /= magnitude;
        avgQuat.z /= magnitude;
        avgQuat.w /= magnitude;

        return avgQuat;
    }
}
