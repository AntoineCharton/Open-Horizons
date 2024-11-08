using System;
using UnityEngine;

public class Traveling : MonoBehaviour
{
    private float speedIncrease = 1000;
    private void Update()
    {
        speedIncrease += Time.deltaTime;
        transform.Translate(Vector3.back * (Time.deltaTime * speedIncrease), Space.World);
    }
}
