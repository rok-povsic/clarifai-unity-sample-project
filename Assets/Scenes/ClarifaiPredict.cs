using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using UnityEngine;

using Clarifai.API;
using Clarifai.DTOs.Inputs;
using Clarifai.DTOs.Predictions;

public class ClarifaiPredict : MonoBehaviour
{

    /// <summary>
    /// Your Clarifai API key.
    /// </summary>
//    private readonly string _clarifaiApiKey = "YOUR-API-KEY";
    private readonly string _clarifaiApiKey = "abe1d99193db4eefa6975fa812a0d55c";

    /// <summary>
    /// The Clarifai client that exposes all methods available.
    /// </summary>
    private ClarifaiClient _client;

    /// <summary>
    /// Queue where concepts are added that are going to be placed on screen.
    /// </summary>
    private readonly List<string> _guiQueue = new List<string>();

    /// <summary>
    /// The displayed concepts in screen and their positions.
    /// </summary>
    private List<Tuple<string, float>> _strAndHeight = new List<Tuple<string, float>>();

    /// <summary>
    /// Whether a new Clarifai request is allowed at this point or not.
    /// </summary>
    private bool _allowNewRequest = true;

    /// <summary>
    /// The style of the GUI labels showing predicted concepts.
    /// </summary>
    private GUIStyle _labelStyle;

    /// <summary>
    /// The time delta per FixedUpdate.
    /// </summary>
    private float _fixedUpdateTimeDelta = 0;

    // Use this for initialization
    void Start()
    {
        // We set this callback in order to allow doing HTTPS requests which are done against the
        // Clarifai API endpoint.
        ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallback;

        // You can skip the API key argument if you have an environmental variable set called
        // CLARIFAI_API_KEY that contains your Clarifai API key.
        _client = new ClarifaiClient(_clarifaiApiKey);

        _labelStyle = new GUIStyle
        {
            fontSize = 20,
            normal = {textColor = UnityEngine.Color.white},
        };

        ClarifaiRequestTimer();
    }

    /// <summary>
    /// Calls itself every X seconds to allow a new Clarifai Predict request to be performed
    /// to predict what concepts are on screen.
    /// </summary>
    private void ClarifaiRequestTimer()
    {
        _allowNewRequest = true;
        Invoke("ClarifaiRequestTimer", 3);
    }

    /// <summary>
    /// Called after camera is finished rendering the scene.
    /// We take a screenshot at this point, and then perform the Clarifai Predict request using the
    /// screenshot as an input image.
    /// </summary>
    async void OnPostRender()
    {
        if (_allowNewRequest)
        {
            _allowNewRequest = false;

            byte[] bytes = TakeScreenshot();

            await Task.Run(async () => {

                // Perform the Clarifai Predict request.
                var response = await _client.Predict<Concept>(
                        _client.PublicModels.GeneralModel.ModelID,
                        new ClarifaiFileImage(bytes),
                        maxConcepts: 20)
                    .ExecuteAsync();

                // We lock the object so the list is intact when OnGUI iterates over it.
                lock (_guiQueue)
                {
                    if (response.IsSuccessful)
                    {
                        if (_guiQueue.Count > 8)
                        {
                            _guiQueue.RemoveRange(0, _guiQueue.Count - 8);
                        }

                        foreach (Concept c in response.Get().Data)
                        {
                            string str = string.Format("{0} - {1:N2}%", c.Name, c.Value * 100);
                            _guiQueue.Add(str);
                        }
                    }
                    else
                    {
                        _guiQueue.Add("  Response error details:" + response.Status.ErrorDetails);
                        _guiQueue.Add("  Response description: " + response.Status.Description);
                        _guiQueue.Add("  Response code: " + response.Status.StatusCode);
                        _guiQueue.Add("Clarifai request has not been successful.");
                    }
                }
            });
        }
    }

    /// <summary>
    /// This method is called in fixed time intervals.
    /// </summary>
    void FixedUpdate()
    {
        _fixedUpdateTimeDelta = Time.deltaTime;
    }

    /// <summary>
    /// This method is called for rendering and handling GUI.
    /// We use it to display predicted concepts.
    /// The concept labels slowly move up the screen.
    /// </summary>
    void OnGUI()
    {
        var newData = new List<Tuple<string, float>>();
        lock (_guiQueue)
        {
            if (_guiQueue.Any() && !_strAndHeight.Any(sh => sh.Item2 <= 20))
            {
                int lastIndex = _guiQueue.Count - 1;
                _strAndHeight.Add(Tuple.Create(_guiQueue[lastIndex], 0F));

                _guiQueue.RemoveAt(lastIndex);
            }

            foreach (var tpl in _strAndHeight)
            {
                string str = tpl.Item1;
                float height = tpl.Item2;

                if (height <= 500)
                {
                    float translation = _fixedUpdateTimeDelta * 80;
                    newData.Add(Tuple.Create(str, height + translation));
                }
            }

            _fixedUpdateTimeDelta = 0;

            _strAndHeight = newData;
        }

        foreach (Tuple<string, float> tuple in newData)
        {
            GUI.Label(new Rect(0, 500 - (int)tuple.Item2, 500, 500), tuple.Item1, _labelStyle);
        }
    }

    /// <summary>
    /// Takes a screenshot and returns a PNG-encoded byte array.
    /// </summary>
    private byte[] TakeScreenshot() {
        // Create a texture the size of the screen, RGB24 format.
        int width = Screen.width;
        int height = Screen.height;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        // Read screen contents into the texture.
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        // Encode texture into PNG.
        byte[] bytes = tex.EncodeToPNG();
        Destroy(tex);
        return bytes;
    }

    /// <summary>
    /// HTTPS validation (since the Clarifai API endpoint uses HTTPS).
    /// Source: https://stackoverflow.com/a/33391290/365837
    /// </summary>
    private bool CertificateValidationCallback(System.Object sender, X509Certificate certificate,
        X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        bool isOk = true;
        // If there are errors in the certificate chain,
        // look at each error to determine the cause.
        if (sslPolicyErrors != SslPolicyErrors.None)
        {
            foreach (X509ChainStatus t in chain.ChainStatus)
            {
                if (t.Status == X509ChainStatusFlags.RevocationStatusUnknown)
                {
                    continue;
                }
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                bool chainIsValid = chain.Build((X509Certificate2)certificate);
                if (!chainIsValid)
                {
                    isOk = false;
                    break;
                }
            }
        }
        return isOk;
    }
}
