using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Network;
using ScriptedObjs;
using Unity.AI.Navigation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class MapController : MonoBehaviour
{

    [DoNotSerialize] public Vector3 WeightedCenter;
    public float WanderRadius = 5f;

    [SerializeField] private Vector3 _maxSize = Vector3.negativeInfinity;
    [SerializeField] private Vector3 _minSize = Vector3.positiveInfinity;
    
    public MapObject _mapObject;

    public int TaskCount = 0;

    private NavMeshDataInstance _nmdInstance;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        await UniTask.WaitUntil(() => _mapObject != null);
        await UniTask.WaitUntil(() => NetworkMgr.Instance.currentMatchData != null);
        
        var obj = Instantiate(_mapObject.MapPrefab);


        await UniTask.Delay(10,ignoreTimeScale:true);

        _nmdInstance = NavMesh.AddNavMeshData(_mapObject.MapNavMeshData);
        
        int c = 0;
        int ct = obj.transform.childCount;

        while (c < ct) 
        {
            foreach (Transform o in obj.transform)
            {
                o.parent = transform;
                c++;
            }
        }

        if (NetworkMgr.Instance.IsRoomHost)
        {
            var taskContainer = transform.Find("TaskContainer");
            TaskCount = NetworkMgr.Instance.currentMatchData.TasksToComplete;

            for (int i = 0; i < TaskCount; i++)
            {
                var task = taskContainer.GetChild(Random.Range(0, taskContainer.childCount));

                await NetworkMgr.Instance.Instantiate(Resources.Load<NetworkBehaviour>("TaskObject"),
                    startPosition: task.position);

                DestroyImmediate(task.gameObject);

                await UniTask.NextFrame();
            }
        }


        Destroy(obj);
    }

    private void OnDisable()
    {
        NavMesh.RemoveNavMeshData(_nmdInstance);
    }

    private void OnGUI()
    {
        GUILayout.Space(20);
        GUILayout.Label($"Tasks Completed: {NetworkMgr.Instance.tasksCompleted}/{TaskCount}");
    }

    // Update is called once per frame
    void Update()
    {
        WeightedCenter = transform.position;
        foreach (Transform o in transform)
        {
            if (o.GetComponent<Collider>())
            {
                Vector3 size = o.GetComponent<Collider>().bounds.size;

                WeightedCenter = Vector3.Lerp(WeightedCenter, o.position, size.magnitude / _maxSize.magnitude);

                _maxSize = Vector3.Max(_maxSize, size);
                _minSize = Vector3.Min(_minSize, size);
            }
        }

        WanderRadius = 0;
        foreach (Transform o in transform)
        {
            if (o.GetComponent<Collider>())
            {
                Vector3 size = o.GetComponent<Collider>().bounds.size;
                float distance = Vector3.Distance(o.position + size, WeightedCenter);
                WanderRadius = Mathf.Max(WanderRadius, distance + 2.5f);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(WeightedCenter, WanderRadius);
    }
}
