using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// This class is responsible for handling basic Unity events for all mods (Awake, Start, Update, ...).
/// </summary>
public class ETGModMainBehaviour : MonoBehaviour {

    public static ETGModMainBehaviour Instance;

    public static Stopwatch StartupStopwatch = new Stopwatch();

    public void Awake() {
        StartupStopwatch.Start();
        DontDestroyOnLoad(gameObject);
#pragma warning disable CS0618
        ETGMod.StartCoroutine = StartCoroutine;
#pragma warning restore CS0618
        ETGMod.StartGlobalCoroutine = StartCoroutine;
        ETGMod.StopGlobalCoroutine = StopCoroutine;
        ETGMod.Init();
    }

    public void Start() {
        ETGMod.Start();
        StartupStopwatch.Stop();
        Debug.Log($"MTG startup elapsed: {StartupStopwatch.Elapsed.TotalSeconds}s");
    }

    public void Update() {
        ETGMod.Assets.Packer.Apply();
    }

}
