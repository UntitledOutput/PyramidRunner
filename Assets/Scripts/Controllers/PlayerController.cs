using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using External;
using Managers;
using MyBox;
using Network;
using ScriptedObjs;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Utils;
using InputSystem = Utils.InputSystem;
using Random = UnityEngine.Random;

public class PlayerController : NetworkBehaviour
{
    private static readonly int Move = Animator.StringToHash("Move");
    private static readonly int Sprint = Animator.StringToHash("Sprint");
    private static readonly int Crouch = Animator.StringToHash("Crouch");
    [SerializeField] private Camera _camera;
    private float _cameraMoveAnimationOffset = 0;
    private float _cameraMoveAnimationScale = (1 / 16f);
    private float _targetFOV = 75f;

    private Light _flashLight;
    private float _flashLightFlickerTimer;
    private readonly float _flashLightFlickerInterval = 0.5f;

    private Rigidbody _rigidBody;
    private CapsuleCollider _collider;
    private float _height = 2f;

    private Transform _model;
    
    [SerializeField] private float _health = 1;
    private Canvas _overheadCanvas;
    public ClothingController clothingController;
    private Animator _animator;
    private bool _sprint, _crouch;
    private float _moveValue = 0;

    public bool CanMove = true;
    [SerializeField] public bool ShowModel = true;
    public bool UnlockCamera = false;

    public float TargetFov;
    public float DamagePower = 3f;

    private float _spectateAngle = 0;
    private string _spectateId = "";
    private float _spectateRadius = 5;

    
    public static PlayerController Instance;

    private Vector3 spawnPoint;
    
    public float Health
    {
        get => _health;
        set
        {
            _health = value;
        }
    }

    [SerializeField] private float _fear;
    private float valueUpdateTimer = 0;
    private float valueUpdateCooldown = 0.01f;
    
    
    public float Fear
    {
        get => _fear;
        set
        {
            if (!IsOwnedByClient)
            {
                if (NetworkMgr.Instance.IsRoomHost)
                {
                    if (valueUpdateTimer >= valueUpdateCooldown)
                    {
                        valueUpdateTimer = 0;
                        _ = NetworkMgr.Instance.SendPlayerUpdate(
                            new Dictionary<string, object>
                            {
                                { "fear", value }
                            }, this);
                    }
                }
            }
            else
            {
                _fear = value;
            }
        }
    }

    
    public static async void HandleCosmeticUpdate(string player_id)
    {
        if (player_id == NetworkMgr.Instance.playerId)
            return;
        var playerInfo = await NetworkMgr.Instance.GetPlayerInfo(player_id);

        var object_id = playerInfo["object_id"].ToString();
        var plr = (PlayerController)NetworkMgr.Instance.InstantiatedPrefabs[object_id];
        plr.clothingController.SuitObject = ObjectRegistry.Instance.Clothing[int.Parse(playerInfo["suit"].ToString())];
        plr.clothingController.HatObject = ObjectRegistry.Instance.HatClothing[int.Parse(playerInfo["hat"].ToString())];
        
        plr.clothingController.RecreateClothing();
    }

    private float _damageTimer;
    private float _damageRecover = 1f;
    private float _moveMultiplier = 1f;
    private bool _recoverFromDamage;
    
    public void HandleDamage(Dictionary<string, object> data)
    {
        float damage = float.Parse(data["damage"].ToString());

        if (data.ContainsKey("enemy_id"))
        {
            string enemy_id = data["enemy_id"].ToString();

            if (!NetworkMgr.Instance.InstantiatedPrefabs[enemy_id])
                return;
            
            EnemyController enemy = (EnemyController)NetworkMgr.Instance.InstantiatedPrefabs[enemy_id];

            if (!enemy)
                return;
            
            Vector3 direction = (transform.position - enemy.transform.position).normalized;
            direction.y = 0;

            bool foundLaunchDirection = false;
            int limit = 3;
            int i = 0;
            while (!foundLaunchDirection)
            {

                var ray = new Ray();
                ray.origin = transform.position;
                ray.direction = direction;

                if (!Physics.Raycast(ray, out var hit, 10f, LayerMask.GetMask("Map")))
                {
                    foundLaunchDirection = true;
                }
                else
                {
                    float angle = Vector3.Angle(Vector3.forward, direction);

                    angle += 10f;

                    direction = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle));
                    i++;
                }

                if (i >= limit)
                {
                    direction = transform.forward;
                    break;
                }

            }


            _rigidBody.linearVelocity = Vector3.zero;
            _rigidBody.AddForce(direction * DamagePower, ForceMode.Impulse);

            _recoverFromDamage = true;
            _damageTimer = 0;
        }

        Health -= damage;

    }

    private bool _isDead = false;
    public void HandleDeath()
    {
        ShowModel = false;
        _isDead = true;
    }

    
    private async void Start()
    {

        await UniTask.Delay(50,ignoreTimeScale:true);
        ShowModel = true;
        
        _camera = Camera.main;
        _overheadCanvas = GetComponentInChildren<Canvas>();
        _overheadCanvas.worldCamera = _camera;

        _overheadCanvas.GetComponent<LookAtConstraint>()
            .AddSource(new ConstraintSource() { sourceTransform = _camera.transform, weight = 1});
        
        
        _model = transform.Find("Model");
        _animator = _model.GetComponentInChildren<Animator>();
        clothingController = _model.GetChild(0).AddComponent<ClothingController>();
        _rigidBody = GetComponent<Rigidbody>();
                    
        var playerInfo = await NetworkMgr.Instance.GetPlayerInfo(OwnedId);

        if (IsOwnedByClient)
        {
            _camera.transform.parent = transform;
            _camera.transform.localPosition = new Vector3(0, 1, 0);
            Instance = this;


            _overheadCanvas.gameObject.SetActive(false);

            
            while (!DataManager.Instance.TriedLoading)
            {
                // Await the next frame to prevent blocking the main thread
                await Awaitable.NextFrameAsync();
            }
            clothingController.SuitObject = ObjectRegistry.Instance.Clothing[DataManager.Instance.CurrentSave.suitIndex];
            clothingController.HatObject = ObjectRegistry.Instance.HatClothing[DataManager.Instance.CurrentSave.hatIndex];
            clothingController.RecreateClothing();
            
            ShowModel = false;

            if (SceneManager.GetActiveScene().name == "ConnectTest0")
            {
                var mapController = FindAnyObjectByType<MapController>();

                var playerSpawn = mapController.transform.Find("PlayerSpawnPoint");

                float angle = ((2 * Mathf.PI) / 4) * int.Parse(playerInfo["index"].ToString());


                transform.position = playerSpawn.position + (new Vector3(Mathf.Sin(angle), 0.125f, Mathf.Cos(angle)) * 3f);

                spawnPoint = transform.position;
            } else if (SceneManager.GetActiveScene().name == "HomeSceneTest" && NetworkMgr.Instance.IsConnectedToRoom)
            {
                float angle = ((2 * Mathf.PI) / 4) * int.Parse(playerInfo["index"].ToString());

                transform.position = (new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 3f);

                spawnPoint = transform.position;
            }
        }
        else
        {
            if (_rigidBody)
            {
                _rigidBody.isKinematic = true;
            }
            _overheadCanvas.GetComponentInChildren<TMP_Text>().text = playerInfo["username"].ToString();
            _overheadCanvas.GetComponentInChildren<TMP_Text>().color = BaseUtils.ColorFromHex(playerInfo["color"].ToString());

            clothingController.DeriveFromPlayerInfo(playerInfo);

            
            ShowModel = true;
        }



        


    }

    private void Update()
    {

        if (_recoverFromDamage)
        {
            _moveMultiplier = 0;
            _damageTimer += Time.deltaTime;
            if (_damageTimer >= _damageRecover)
            {
                _recoverFromDamage = false;
                _damageTimer = 0;
            }
        }
        else
        {
            _moveMultiplier += Time.deltaTime*5f;
            _moveMultiplier = Mathf.Clamp(_moveMultiplier, 0, 1);
        }

        valueUpdateTimer += Time.deltaTime;
        
        if (_model) 
            if (_model.gameObject.activeSelf != ShowModel)
                _model.gameObject.SetActive(ShowModel);

        if (_flashLight == null)
        {
            _flashLight = GetComponentInChildren<Light>();
        }

        if (_collider == null)
        {
            _collider = GetComponent<CapsuleCollider>();
        }

        if (_rigidBody == null)
        {
            _rigidBody = GetComponent<Rigidbody>();
        }
        
        if (IsOwnedByClient && _camera)
        {
            Instance = this;

            Fear -= Time.deltaTime;

            if (transform.position.y < -10f)
            {
                transform.position = spawnPoint;
                _rigidBody.linearVelocity = Vector3.zero;
                _ = NetworkMgr.Instance.SendPlayerDamage(10000f, this);
            }

            if (Application.isFocused && CanMove)
            {
                if (!_isDead) {
                    _collider.enabled = true;
                    _flashLight.enabled = true;
                    _rigidBody.isKinematic = false;

                    if (!Keyboard.current.pKey.isPressed)
                    {
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Locked;
                    } else {
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;
                    }
                    transform.eulerAngles += new Vector3(0, InputSystem.Look.x * 15f, 0);
                    _camera.transform.localEulerAngles += new Vector3(InputSystem.Look.y * 15f, 0, 0);

                    var moveDir = new Vector3(InputSystem.Move.x, 0, InputSystem.Move.y);
                    moveDir = transform.TransformDirection(moveDir);

                    float speed = 3f;
                    float animationSpeed = 0.05f;
                    _targetFOV = 75f;
                    _height = 2f;
                    _cameraMoveAnimationScale = (1 / 16f);

                    if (InputSystem.Sprint)
                    {
                        speed = 7.5f;
                        animationSpeed = 0.15f;
                        _targetFOV = 95f;
                        _cameraMoveAnimationScale = (1 / 2f);
                    }
                    else if (InputSystem.Crouch)
                    {
                        speed = 1.5f;
                        animationSpeed = 0.075f;
                        _targetFOV = 60f;
                        _height = 1.25f;
                    }
                    

                    _crouch = InputSystem.Crouch;
                    _sprint = InputSystem.Sprint;

                    velocity = moveDir;
                    if (!_recoverFromDamage)
                        transform.position += velocity  * (speed * Time.deltaTime * _moveMultiplier);


                    _cameraMoveAnimationOffset += InputSystem.Move.magnitude * animationSpeed;
                    TargetFov = _targetFOV;
                } else
                {
                    ShowModel = false;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    velocity = Vector3.zero;

                    _rigidBody.isKinematic = true;
                    _collider.enabled = false;
                    _flashLight.enabled = false;
                }
            }
            else
            {
                velocity = Vector3.zero;
                _targetFOV = TargetFov;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (!UnlockCamera)
            {
                if (!_isDead) {
                    _camera.transform.parent = transform;
                    // camera move animation
                    var cameraY = _height - (_height / 4f);


                    cameraY += (Mathf.Sin(_cameraMoveAnimationOffset)) * _cameraMoveAnimationScale;

                    _camera.transform.localPosition = Vector3.Lerp(_camera.transform.localPosition,
                        new Vector3(0, cameraY, 0),
                        Time.deltaTime * 3f);

                    _camera.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(_camera.transform.localEulerAngles),
                        Quaternion.Euler(_camera.transform.localEulerAngles.SetYZ(0, 0)), Time.deltaTime * 3f);
                } else
                {
                    if (_spectateId.IsNullOrEmpty())
                    {
                        if (NetworkMgr.Instance.PlayerControllers.Count > 1)
                        {
                            foreach (var player in NetworkMgr.Instance.PlayerControllers)
                            {
                                if (player.Key != OwnedId)
                                {
                                    _spectateId = player.Key;
                                }
                            }
                        }
                    }

                    if (!_spectateId.IsNullOrEmpty() && NetworkMgr.Instance.PlayerControllers.TryGetValue(_spectateId, out var spectatedPlayer))
                    {

                        _spectateAngle += InputSystem.Look.x;
                        
                        var spectateOffset = new Vector3();
                        spectateOffset.y = 2;

                        spectateOffset.x = Mathf.Sin(_spectateAngle)*_spectateRadius;
                        spectateOffset.z = Mathf.Cos(_spectateAngle)*_spectateRadius;

                        if (Physics.Raycast(spectatedPlayer.transform.position + new Vector3(0,2,0), spectateOffset.normalized, out RaycastHit hit,
                                7.5f))
                        {
                            _spectateRadius = Mathf.Lerp(_spectateRadius, hit.distance - 0.25f, Time.deltaTime * 10f);
                            
                            spectateOffset.x = Mathf.Sin(_spectateAngle)*_spectateRadius;
                            spectateOffset.z = Mathf.Cos(_spectateAngle)*_spectateRadius;
                        }

                        _camera.transform.position = spectatedPlayer.transform.position + spectateOffset;
                        _camera.transform.LookAt(spectatedPlayer.transform.position + new Vector3(0,1,0));
                        TargetFov = 60;
                    }
                }
            }
            else
            {
                if (_camera.transform.parent == transform)
                    _camera.transform.parent = null;
                
            }
            
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFOV, Time.deltaTime * 3f);

        }
        else
        {
            _rigidBody.isKinematic = true;
        }
        
        _moveValue = Mathf.Lerp(_moveValue,velocity.magnitude, Time.deltaTime * 3f);

        if (_animator != null)
        {
            _animator.SetFloat(Move, _moveValue);
            _animator.SetBool(Sprint, _sprint);
            _animator.SetBool(Crouch, _crouch);
        }

        if (_flashLight)
        {
            // flashlight flicker
            _flashLightFlickerTimer += Time.deltaTime;
            if (_flashLightFlickerTimer >= _flashLightFlickerInterval)
            {
                _flashLight.intensity = 20 * Random.Range(0.5f, 1f);
                _flashLightFlickerTimer = 0;
            }
        }


        if (_collider)
        {
            // capsule stuff
            _collider.height = Mathf.Lerp(_collider.height, _height, Time.deltaTime * 3f);
            _collider.center = new Vector3(0, Mathf.Lerp(_collider.center.y, _height / 2f, Time.deltaTime * 3f), 0);
        }
    }

    internal override void OnDisconnect()
    {
        base.OnDisconnect();
        if (IsOwnedByClient)
        {
            _camera.transform.parent = null;
            _camera.transform.position = new Vector3(0, 5, -10);
            _camera.transform.LookAt(new Vector3(0, 0, 0));
        }
    }

    private void OnDisable()
    {
        Instance = null;
    }

    internal override async Task<Dictionary<string, object>> Serialize()
    {
        if (IsOwnedByClient)
        {
            extraData["sprint"] = _sprint;
            extraData["crouch"] = _crouch;
        }

        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["fear"] = Fear;
            
            await NetworkMgr.Instance.SendPlayerUpdate(data);   
        }
        
        var packet = base.Serialize();
        
        return await packet;
    }

    internal override void OnNetworkUpdate(Dictionary<object, object> packet)
    {
        base.OnNetworkUpdate(packet);

        if (extraData.ContainsKey("sprint"))
        {
            _sprint = (bool)extraData["sprint"];
            _crouch = (bool)extraData["crouch"];
        }
    }
}
