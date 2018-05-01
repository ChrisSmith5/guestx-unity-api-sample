using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System;
using System.Text;
using Boomlagoon.JSON;



public class MainController : MonoBehaviour
{

    // download url
    string downloadUrl = "https://us-central1-expressions-booth-guestx.cloudfunctions.net/getPhotos?lastRecordId=";

    // upload API url
    string uploadUrl = "http://api.expressions.qa.guestx.com/v1/media";

    int lastId = 0; // latest request id

    string lastFetchedDownloadJSON = "";

    // Use this for initialization
    void Start()
    {
        //check have we latestId or not, if not set 0
        if (PlayerPrefs.HasKey("lastId"))
        {
            lastId = PlayerPrefs.GetInt("lastId");
        }
        else
        {
            lastId = 0;
            PlayerPrefs.SetInt("lastId", 0);
        }

        //check new Photo Data every 60 seconds
        InvokeRepeating("CheckNewPhotos", 0.0f, 60.0f);
    }

    void CheckNewPhotos()
    {
        Debug.Log("Start check" + DateTime.Now);

        try
        {
            WWW api = new WWW(downloadUrl + lastId);
            //Debug.Log(downloadUrl + lastId);
            StartCoroutine(CheckWait(api));
        }
        catch (UnityException ex)
        {
            //Debug.Log(ex.Message);
        }
    }

    IEnumerator CheckWait(WWW api)
    {
        yield return api;
        if (api.error != null)
        {
            //Debug.LogError("Error: " + api.error + " /// Details: " + api.text);
        }
        else
        {
            if (api.text != "")
            {
                //Debug.Log("Response: " + api.text);
                // DATA with latest uploaded photos
                lastFetchedDownloadJSON = api.text;
                parseJson();
                yield return new WaitForEndOfFrame();
            }
            else
            {
                //Debug.Log("No Response from Register Device API");
            }
        }
    }

    // PARSE JOSN DATA FROM FIRBASE
    public void parseJson()
    {
        JSONObject jsonObject = JSONObject.Parse(lastFetchedDownloadJSON);

        foreach (KeyValuePair<string, JSONValue> pair in jsonObject)
        {

            JSONObject item = JSONObject.Parse(pair.Value.ToString());
            //Debug.Log("Object #" + pair.Key.ToString());


            var ticketBarcode = item.GetString("ticketBarcode");
            //Debug.Log(ticketBarcode);


            var fileUrl = item.GetString("fileUrl");
            //Debug.Log(fileUrl);


            var recordId = item.GetNumber("recordId");
            //Debug.Log(recordId);


            //save latest id
            if (recordId > lastId)
            {
                lastId = (int)recordId;
                PlayerPrefs.SetInt("lastId", lastId);

            }

            DownloadImage(fileUrl, ticketBarcode);
        }
    }

    public void DownloadImage(string url, string ticketBarcode)
    {
        try
        {
            WWW api = new WWW(url);
            StartCoroutine(DownloadImageWait(api, ticketBarcode));
        }
        catch (UnityException ex)
        {
            //Debug.Log(ex.Message);
        }
    }

    IEnumerator DownloadImageWait(WWW api, string ticketBarcode)
    {

        yield return api;
        if (api.error != null)
        {
            //Debug.LogError("Download image Error: // " + api.error + "/// Details: " + api.text);
        }
        else
        {
            //Debug.Log("Download image  Response: // " + api.text);
            DownloadImageSuccess(api, ticketBarcode);
        }
    }

    void DownloadImageSuccess(WWW api, string ticketBarcode)
    {
        //Debug.Log("Save Image");
        //create texture for save file
        Texture2D texture = new Texture2D(1, 1);
        api.LoadImageIntoTexture(texture);
        var bytes = texture.EncodeToPNG();

        //create name for file "ticketBarcode_currentTime.jpg", add to path
        DateTime now = DateTime.Now;
        var fileName = ticketBarcode.ToString() + "_" + now.Ticks.ToString();
        FileStream file = File.Open(Application.streamingAssetsPath + "/" + fileName + ".jpg", FileMode.Create);

        //save data in file
        BinaryWriter bw = new BinaryWriter(file);
        bw.Write(bytes);
        file.Close();
        StartCoroutine(UploadVideo(ticketBarcode));
    }

    IEnumerator UploadVideo(string ticketBarcode)
    {
        var dataAPI = "[{" +
    "\"name\": \"\"," +
    "\"description\": \"\"," +
    "\"title\": \"\"," +
    "\"tags\": [\"morphVideo\"]," +
    "\"captureType\": 10810," +
    "\"mediaType\": \"Video\"," +
            "\"deviceToken\": \"\"," +
            "\"type\": 10102," +
    "\"visibility\": 10301," +
            "\"ticketBarcodes\": [\"" +  ticketBarcode + "\"]," +
    "\"guestId\": \"\"," +
    "\"data\": {}" +
    "}]";


        // create data for request
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormDataSection("info", dataAPI));

        // video file to bytes 
        byte[] videoBytes = File.ReadAllBytes(Application.dataPath + "/video.mp4");
        formData.Add(new MultipartFormFileSection("files", videoBytes, "video.mp4", "video/mp4"));


        // headers for post api
        Hashtable postHeader = new Hashtable();
        byte[] boundary = UnityWebRequest.GenerateBoundary();
        byte[] formSections = UnityWebRequest.SerializeFormSections(formData, boundary);

        // create request
        UnityWebRequest www = UnityWebRequest.Post(uploadUrl, formData);
        byte[] terminate = Encoding.UTF8.GetBytes(String.Concat("\r\n--", Encoding.UTF8.GetString(boundary), "--"));
        byte[] body = new byte[formSections.Length + terminate.Length];

        // buffer for check length
        Buffer.BlockCopy(formSections, 0, body, 0, formSections.Length);
        Buffer.BlockCopy(terminate, 0, body, formSections.Length, terminate.Length);

        // multipart header
        UploadHandler uploader = new UploadHandlerRaw(body); // new UploadHandlerRaw(formSections);
        uploader.contentType = String.Concat("multipart/form-data; boundary=", Encoding.UTF8.GetString(boundary));
        www.uploadHandler = uploader;

        // send request
        yield return www.Send();
        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log("Download source Error: // " + www.error + "/// Details: " + www.downloadHandler.text + "///" + www.responseCode);
        }
        else
        {
            Debug.Log("Form upload complete!" + www.downloadHandler.text);
        }
    }

}

