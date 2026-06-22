using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DefaultNamespace;
using External;
using MyBox;
using Network;
using ScriptedObjs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GameMgr : MonoBehaviour
{
    private static readonly int VignetteMultiplier = Shader.PropertyToID("_VignetteMultiplier");
    public GameObject OpeningAnimPrefab;
    public Vector3 OpeningAnimOffset;
    
    public GameObject ClosingAnimPrefab;
    public Vector3 ClosingAnimOffset;
    
    public GameObject ShipPrefab;
    
    public Canvas GameCanvas;
    
    private Camera _camera;
    private LayerMask _cameraCullingMask;
    
    private Transform _openingAnimFOV, _openingAnimCam, _openingAnimFade;
    private Animator _openingAnimAnimator;
    private bool _isPlayingOpeningAnim = false;
    private GameObject _openAnimContainer;

    private bool _isPlayingClosingAnim = false;
    private Transform _closingAnimFOV, _closingAnimCam, _closingAnimFade;
    private Animator _closingAnimAnimator;
    private GameObject _closeAnimContainer;
    
    private Slider HealthSlider, StaminaSlider, FearSlider;
    private Material _fearOverlayMaterial;

    private MapController _mapController;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        await UniTask.WaitUntil(() => NetworkMgr.Instance.currentMatchData != null);
        
        _mapController =  FindAnyObjectByType<MapController>();
        _mapController._mapObject = NetworkMgr.Instance.currentMatchData.Map;
        
        _fearOverlayMaterial = Resources.Load<Material>("Materials/FearScreen");
        HealthSlider = GameCanvas.transform.RecursiveFind<Slider>("HealthSlider");
        StaminaSlider = GameCanvas.transform.RecursiveFind<Slider>("StaminaSlider");
        FearSlider = GameCanvas.transform.RecursiveFind<Slider>("FearSlider");

        await UniTask.Delay(1000);

        var player = PlayerController.Instance;
        if (player != null)
        {
            player.UnlockCamera = true;
            player.CanMove = false;
        }

        _camera = Camera.main;

        // open animation setup
        await SetupIntroAnimation();


    }
    
    public async Task SetupIntroAnimation()
    {
                
        _openAnimContainer = new GameObject();
        _openAnimContainer.transform.position =  OpeningAnimOffset;

        await UniTask.Delay(1,ignoreTimeScale:true);


        var openingAnimationObj = Instantiate(OpeningAnimPrefab, _openAnimContainer.transform);

        await UniTask.Delay(1,ignoreTimeScale:true);


        _openingAnimAnimator = openingAnimationObj.GetComponent<Animator>();
        _openingAnimFOV = openingAnimationObj.transform.Find("FovControl");
        _openingAnimFade = openingAnimationObj.transform.Find("FadeControl");

        var dir = openingAnimationObj.transform.Find("MapRoot").position - Vector3.zero;
        _openAnimContainer.transform.position -= dir;

        for (int i = 0; i <= NetworkMgr.Instance.PlayerControllers.Count; i++)
        {
            var ship = Instantiate(ShipPrefab, openingAnimationObj.transform.Find($"Ship{i:00}"));
            ship.transform.localScale = Vector3.one * 0.75f;
            ship.transform.localEulerAngles = new Vector3(-90, -90, 0);
        }
        
        _openingAnimCam = openingAnimationObj.transform.Find("Camera");
        
        _camera.transform.parent = null;
        _camera.transform.localPosition = Vector3.zero;
        _camera.transform.localEulerAngles = new Vector3(0, 180, 0);
        _camera.transform.localScale = Vector3.one;
        _cameraCullingMask = _camera.cullingMask;
        _camera.cullingMask = LayerMask.GetMask("DbgRenderLayer");
        
        var previewMap = Instantiate(NetworkMgr.Instance.currentMatchData.Map.PreviewPrefab, openingAnimationObj.transform);
        previewMap.transform.position = Vector3.zero;
        previewMap.transform.up = Vector3.up;
        
        _openAnimContainer.SetLayerRecursively(LayerMask.NameToLayer("DbgRenderLayer"));
            
        _isPlayingOpeningAnim = true;

    }

    public async Task SetupCloseAnimation()
    {
        FadeController.Instance.FadeAmount = 1;
        FadeController.Instance.FadeSpeed = 2;

        await UniTask.Delay(2500);
        
        
        var player = PlayerController.Instance;
        if (player != null)
        {
            player.UnlockCamera = true;
            player.CanMove = false;
        }
        
        FadeController.Instance.FadeAmount = 0;
        FadeController.Instance.FadeSpeed = 2;
        
        _closeAnimContainer = new GameObject();
        _closeAnimContainer.transform.position = ClosingAnimOffset;

        await UniTask.Delay(1,ignoreTimeScale:true);


        var closingAnimationObj = Instantiate(ClosingAnimPrefab, _closeAnimContainer.transform);

        await UniTask.Delay(1,ignoreTimeScale:true);


        _closingAnimAnimator = closingAnimationObj.GetComponent<Animator>();
        _closingAnimFOV = closingAnimationObj.transform.Find("FovControl");
        //_closingAnimFade = closingAnimationObj.transform.Find("FadeControl");

        for (int i = 0; i <= NetworkMgr.Instance.PlayerControllers.Count; i++)
        {
            var ship = Instantiate(ShipPrefab, closingAnimationObj.transform.Find($"Player{i:00}"));
            ship.transform.localScale = Vector3.one * 0.75f;
            ship.transform.localEulerAngles = new Vector3(-90, -90, 0);
        }
        
        _closingAnimCam = closingAnimationObj.transform.Find("Camera");
        
        _camera.transform.parent = _closingAnimCam;
        _camera.transform.localPosition = Vector3.zero;
        _camera.transform.localEulerAngles = new Vector3(0, 180, 0);
        _camera.transform.localScale = Vector3.one;
        _cameraCullingMask = _camera.cullingMask;
        _camera.cullingMask = LayerMask.GetMask("DbgRenderLayer");
        

        _closeAnimContainer.SetLayerRecursively(LayerMask.NameToLayer("DbgRenderLayer"));
            
        
        _isPlayingClosingAnim = true;

        
        
        while (_isPlayingClosingAnim)
        {
            GameCanvas.enabled = false;
            float focalLength = ((_closingAnimFOV.localPosition.z*100f) * 25.0f) + 50.0f;
            float sensorHeight = 24f; // Default height for 36mm sensor width
            PlayerController.Instance.TargetFov =
                2.0f * Mathf.Atan(sensorHeight / (2.0f * focalLength)) * Mathf.Rad2Deg;
            _camera.fieldOfView = PlayerController.Instance.TargetFov;
            
            //FadeController.Instance.FadeAmount = _openingAnimFade.localPosition.z*100;
            //FadeController.Instance.FadeSpeed = 100;
            
            if (_closingAnimAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1)
            {
                Debug.Log("finished closing anim");
                _isPlayingClosingAnim = false;
            }

            await UniTask.NextFrame();
        }
        
        await UniTask.Delay(3000,ignoreTimeScale:true);
        
        
        if (NetworkMgr.Instance.IsRoomHost)
        {
            await NetworkMgr.Instance.EndGame(true);
        }
    }

    IEnumerator CloseIntroAnimation()
    {
        _isPlayingOpeningAnim = false;
        yield return new WaitForSeconds(1f);
        _camera.cullingMask = _cameraCullingMask;
        _camera.transform.parent = null;
        Destroy(_openAnimContainer.gameObject);
        if (PlayerController.Instance)
        {
            PlayerController.Instance.UnlockCamera = false;
            PlayerController.Instance.CanMove = true;
            _camera.transform.position = PlayerController.Instance.transform.position;
        }

        GameCanvas.enabled = true;
        FadeController.Instance.FadeAmount = 0;
        FadeController.Instance.FadeSpeed = 2;
        
        if (NetworkMgr.Instance.IsRoomHost)
        {
            yield return NetworkMgr.Instance.StartGameTimer();

            yield return new WaitForSecondsRealtime(10f);
            
            if(!_isPlayingClosingAnim)
                yield return NetworkMgr.Instance.Instantiate(Resources.Load<NetworkBehaviour>("Enemy"), startPosition:_mapController.transform.Find("EnemySpawnPoint").position);
        }
    }

    public void HandleGameComplete(bool victory)
    {
        IEnumerator CompleteGame()
        {
            if (NetworkMgr.Instance.IsRoomHost)
            {
                var enemy = FindFirstObjectByType<EnemyController>();
                Destroy(enemy);
            }

            if (victory)
                yield return SetupCloseAnimation();
            else
            {
                if (NetworkMgr.Instance.IsRoomHost)
                {
                    yield return NetworkMgr.Instance.EndGame(false);
                }
            }
            
        }

        if (NetworkMgr.Instance.IsRoomHost)
        {
            _ = NetworkMgr.Instance.SendPlayerState("end_game_seq", new Dictionary<string, object>
            {
                { "victory", victory }
            });
        }

        StartCoroutine(CompleteGame());
    }

    // Update is called once per frame
    void Update()
    {
        if (!NetworkMgr.Instance.IsConnectedToRoom)
        {
            SceneManager.LoadScene("HomeSceneTest");
        }
        
        if (_isPlayingOpeningAnim)
        {
            GameCanvas.enabled = false;
            float focalLength = (_openingAnimFOV.localPosition.z*100)+55f;
            float sensorHeight = 24f; // Default height for 36mm sensor width
            PlayerController.Instance.TargetFov = 2.0f * Mathf.Atan(sensorHeight / (2.0f * focalLength)) * Mathf.Rad2Deg;
            _camera.fieldOfView = PlayerController.Instance.TargetFov;
            
            float cameraLerpSpeed = Time.deltaTime * 2f;
            _camera.transform.position = Vector3.Slerp(_camera.transform.position, _openingAnimCam.transform.position, cameraLerpSpeed);
            _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, _openingAnimCam.transform.rotation * Quaternion.Euler(new Vector3(0,180,0)), cameraLerpSpeed);

            FadeController.Instance.FadeAmount = _openingAnimFade.localPosition.z*100;
            FadeController.Instance.FadeSpeed = 100;
            
            if (_openingAnimAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1)
            {
                _isPlayingOpeningAnim = false;
                StartCoroutine(CloseIntroAnimation());

            }
        }

        if (HealthSlider && PlayerController.Instance)
        {
            float lerpSpeed = Time.deltaTime * 10f;

            float health = PlayerController.Instance.Health;
            float fear = PlayerController.Instance.Fear;
            
            HealthSlider.value = Mathf.Lerp(HealthSlider.value,health,lerpSpeed);
            FearSlider.value = Mathf.Lerp(FearSlider.value,fear+(Random.Range(0,1000f)/1000f)*(Mathf.Clamp((fear-0.5f)*3f,0,Mathf.Infinity)) ,lerpSpeed);
                    
        
            _fearOverlayMaterial.SetFloat(VignetteMultiplier, FearSlider.value*100f);
        }
        
                    

    }
    
    private void OnDisable()
    {
        if (_fearOverlayMaterial)
            _fearOverlayMaterial.SetFloat(VignetteMultiplier,0);
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(OpeningAnimOffset,1f);
    }
}
