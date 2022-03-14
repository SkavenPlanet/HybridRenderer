using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightSpawner : MonoBehaviour
{
    public class LightPos
    {
        public GameObject light;
        public Vector3 targetPosition;

        public LightPos (GameObject l, Vector3 tg)
        {
            light = l;
            targetPosition = tg;
        }
    }

    public GameObject pointLightPrefab, spotLightPrefab;
    public int numPointLights = 15;
    public int numSpotLights = 15;
    //public int displayNumLights;
    public bool moveLights = false;
    List<LightPos> pointLights = new List<LightPos>();
    List<LightPos> spotLights = new List<LightPos>();
    public Vector3 boundingBox;
    // Start is called before the first frame update
    void Start()
    {
        for(int i = 0; i < numPointLights; i++)
        {
            GameObject obj = Object.Instantiate(pointLightPrefab);
            obj.GetComponent<Light>().color = Random.ColorHSV(0, 1, 1, 1, 1, 1);
            obj.transform.position = GetRandomPoint();
            obj.SetActive(true);
            pointLights.Add(new LightPos(obj, GetRandomPoint()));
        }
        for (int i = 0; i < numSpotLights; i++)
        {
            GameObject obj = Object.Instantiate(spotLightPrefab);
            obj.transform.forward = -Vector3.up;
            obj.GetComponent<Light>().color = Random.ColorHSV(0, 1, 1, 1, 1, 1);
            obj.transform.position = GetRandomPoint(3);
            obj.SetActive(true);
            spotLights.Add(new LightPos(obj, GetRandomPoint()));
        }
    }

    Vector3 GetRandomPoint (float y = 2)
    {
        Vector3 p = new Vector3(Random.Range(-boundingBox.x, boundingBox.x), y, 
            Random.Range(-boundingBox.z, boundingBox.z));
        return p;
    }

    // Update is called once per frame
    void Update()
    {
        if (moveLights)
        {
            foreach (var lightPos in pointLights)
            {
                Vector3 toTarget = (lightPos.targetPosition - lightPos.light.transform.position);
                lightPos.light.transform.position +=
                    toTarget.normalized * Time.deltaTime;
                if (toTarget.magnitude < 0.1f)
                {
                    lightPos.targetPosition = GetRandomPoint();
                }
            }
            foreach (var lightPos in spotLights)
            {
                Vector3 toTarget = (lightPos.targetPosition - lightPos.light.transform.position);
                lightPos.light.transform.position +=
                    toTarget.normalized * Time.deltaTime;
                if (toTarget.magnitude < 0.1f)
                {
                    lightPos.targetPosition = GetRandomPoint();
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(Vector3.zero, boundingBox*2);
    }
}
