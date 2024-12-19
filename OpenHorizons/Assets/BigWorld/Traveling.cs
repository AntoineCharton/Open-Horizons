using System;
using UnityEngine;

public class Traveling : MonoBehaviour
{
    private float speedIncrease = 0.000000000001f;
    private void Update()
    {
        speedIncrease = 500;
        transform.Translate(Vector3.back * (Time.deltaTime * speedIncrease), Space.World);
    }
}
