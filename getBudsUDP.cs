using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
 * Modified from........
 *
 * Author: Hyung-il Kim
 * M.S. Student, KAIST UVR Lab.
 * 
 * Receives rotation vector as quaternion, by UDP comm.
 * x, y, z, w
 * 
 * - Recenter rotation using space bar
 * 
 * Modified by xlinka to include quaternion smoothing and consistency check.
 */

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class getBudsUDP : MonoBehaviour {

    // receiving Thread
    Thread receiveThread;

    // udpclient object
    UdpClient client;

    // port number
    public String TARGET_IP = "127.0.0.1";
    public int TARGET_PORT = 6969;

    public int RECEIVING_PORT = 12562;

    public float ID = -1;
    public Vector3 gyro = new Vector3(.0f, .0f, .0f);
    public Vector3 acc = new Vector3(.0f, .0f, .0f);
    public Vector4 rot = new Vector4(.0f, .0f, .0f, .0f);

    public int sent_num = 0;

    // Variables for smoothing and consistency check (added by xlinka)
    private Vector4 lastRot = new Vector4(0f, 0f, 0f, 1f); // Initial quaternion for consistency check
    public float smoothFactor = 5.0f; // Adjust for more or less smoothing

    public getBudsUDP(int port) {
        this.RECEIVING_PORT = port;
    }

    IEnumerator Start() {
        Debug.Log("UDPSendReceive: Starting");
        this.init();

        yield return new WaitForSeconds(1.0f);
    }

    void Update() {
        if (Input.GetKeyDown("space")) {
            print("space key was pressed");
            sent_num += 1;

            UdpClient udpClient = new UdpClient(TARGET_IP, TARGET_PORT);
            Debug.Log("UDPSendReceive: sending" + TARGET_IP + ":" + TARGET_PORT);

            byte[] sendBytes2 = ConvertDoubleToByte(new double[] { sent_num, sent_num * 0.3, sent_num * 0.5 });

            try {
                udpClient.Send(sendBytes2, sendBytes2.Length);
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
        }

        // Smoothly interpolate rotation using Quaternion.Slerp (added by xlinka)
        Quaternion currentRotation = transform.rotation;
        Quaternion targetRotation = new Quaternion(rot.x, rot.y, rot.z, rot.w);
        transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, Time.deltaTime * smoothFactor);
    }

    // OnDestroy
    public void OnDestroy() {
        receiveThread.Abort();
        client.Close();
        Debug.Log("UDPSendReceive: OnDestroy");
    }

    // init
    private void init() {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = false;
        receiveThread.Start();
    }

    public static float[] ConvertByteToFloat(byte[] array) {
        float[] floatArr = new float[10];
        for (int i = 0; i < floatArr.Length; i++) {
            floatArr[i] = BitConverter.ToSingle(array, i * 4);
        }
        return floatArr;
    }

    public static byte[] ConvertDoubleToByte(double[] array) {
        byte[] byte_arr = new byte[8 * array.Length];
        for (int i = 0; i < array.Length; i++) {
            byte[] bytes = BitConverter.GetBytes(array[i]);
            Array.Copy(bytes, 0, byte_arr, 8 * i, bytes.Length);
        }
        return byte_arr;
    }

    // receive thread
    private void ReceiveData() {
        client = new UdpClient(RECEIVING_PORT);
        Debug.Log("UDPSendReceive: listening at port.... " + RECEIVING_PORT);
        while (true) {
            try {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                string receiveStr = Encoding.UTF8.GetString(data);

                string[] subs = receiveStr.Split(',');
                Vector4 newRot = new Vector4(
                    float.Parse(subs[0]),
                    float.Parse(subs[1]),
                    float.Parse(subs[2]),
                    float.Parse(subs[3])
                );

                // Check for quaternion consistency (added by xlinka)
                float dot = Vector4.Dot(lastRot, newRot);
                if (dot < 0.0f) {
                    // Negate the quaternion if dot product is negative
                    newRot = new Vector4(-newRot.x, -newRot.y, -newRot.z, -newRot.w);
                }

                // Update rotation and last rotation for the next frame
                rot = newRot;
                lastRot = newRot;

            } catch (Exception err) {
                Debug.LogError(err.ToString());
            }
        }
    }
}
