using System;
using System.Collections;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Network;
using UnityEngine;

namespace Managers
{
    public class HomeMgr : MonoBehaviour
    {
        private async void Start()
        {
            while (!NetworkMgr.Instance.IsConnected)
            {
                // Await the next frame to prevent blocking the main thread
                await Awaitable.NextFrameAsync();
            }

            await NetworkMgr.Instance.Instantiate(Resources.Load<NetworkBehaviour>("Player"));

            /*
            await UniTask.Delay(100,ignoreTimeScale:true);
            
            await NetworkMgr.Instance.JoinRoom("test_room");

            await UniTask.Delay(100,ignoreTimeScale:true);

            
            await NetworkMgr.Instance.StartGame();
            
            await UniTask.Delay(100,ignoreTimeScale:true);
            */
        }

        public GameObject OutfitTrigger, OnlineTrigger;

        public GameObject OutfitMenu, OnlineMenu;

        public RoomScreenMgr RoomScreenMgr;

        public void ChangeOutfit()
        {
            OutfitTrigger.GetComponentInChildren<Canvas>().enabled = false;

            var player = PlayerController.Instance;
            var camera = Camera.main;

            player.CanMove = false;

            IEnumerator ChangeOutfitEnter()
            {
                player.ShowModel = true;
                player.UnlockCamera = true;
                player.TargetFov = 45;

                Vector3 targetPlayerPosition = OutfitTrigger.transform.position;
                Vector3 targetCameraPosition = OutfitTrigger.transform.position +
                                               (OutfitTrigger.transform.forward * 3f) +
                                               (OutfitTrigger.transform.up * 1f) +
                                               (-OutfitTrigger.transform.right * 1f);



                while (
                    Vector3.Distance(player.transform.position, targetPlayerPosition) > 0.01f &&
                    Vector3.Distance(camera.transform.position, targetCameraPosition) > 0.01f
                )
                {
                    float transformSpeed = 7.5f * Time.deltaTime;

                    player.transform.position =
                        Vector3.Lerp(player.transform.position, targetPlayerPosition, transformSpeed);


                    Quaternion playerRotation = player.transform.rotation;
                    player.transform.forward = OutfitTrigger.transform.forward;

                    player.transform.rotation =
                        Quaternion.Slerp(playerRotation, player.transform.rotation, transformSpeed);

                    camera.transform.position =
                        Vector3.Lerp(camera.transform.position, targetCameraPosition, transformSpeed);
                    Quaternion cameraRotation = camera.transform.rotation;
                    camera.transform.LookAt(targetPlayerPosition + (OutfitTrigger.transform.up * 1f) +
                                            (-OutfitTrigger.transform.right * 1f));

                    camera.transform.rotation =
                        Quaternion.Slerp(cameraRotation, camera.transform.rotation, transformSpeed);

                    yield return null;
                }

                player.transform.position = targetPlayerPosition;
                camera.transform.position = targetCameraPosition;
                OutfitMenu.gameObject.SetActive(true);
            }

            StartCoroutine(ChangeOutfitEnter());
        }

        public void CloseOutfit()
        {
            OutfitTrigger.GetComponentInChildren<Canvas>().enabled = true;

            var player = PlayerController.Instance;
            var camera = Camera.main;


            IEnumerator ChangeOutfitExit()
            {
                yield return null;

                Vector3 targetPlayerPosition =
                    OutfitTrigger.transform.position + (OutfitTrigger.transform.forward * 5f);



                while (
                    Vector3.Distance(player.transform.position, targetPlayerPosition) > 0.01f &&
                    Vector3.Distance(camera.transform.position, targetPlayerPosition) > 0.01f
                )
                {
                    float transformSpeed = 3f * Time.deltaTime;

                    player.transform.position =
                        Vector3.Lerp(player.transform.position, targetPlayerPosition, transformSpeed);

                    camera.transform.position =
                        Vector3.Lerp(camera.transform.position, targetPlayerPosition, transformSpeed);

                    camera.transform.forward =
                        Vector3.Lerp(camera.transform.forward, player.transform.forward, transformSpeed);
                }

                player.transform.position = targetPlayerPosition;

                player.ShowModel = false;
                player.CanMove = true;
                player.UnlockCamera = false;

                OutfitMenu.gameObject.SetActive(false);
                yield return DataManager.Instance.Save();
            }

            StartCoroutine(ChangeOutfitExit());
        }

        public void OpenRoomMenu()
        {
            OnlineTrigger.GetComponentInChildren<Canvas>().enabled = false;

            var player = PlayerController.Instance;
            var camera = Camera.main;

            player.CanMove = false;

            IEnumerator ChangeOnlineEnter()
            {
                player.ShowModel = true;
                player.UnlockCamera = true;
                player.TargetFov = 45;

                Vector3 targetPlayerPosition = OnlineTrigger.transform.position;
                Vector3 targetCameraPosition = OnlineTrigger.transform.position +
                                               (OnlineTrigger.transform.forward * 3f) +
                                               (OnlineTrigger.transform.up * 1f) +
                                               (-OnlineTrigger.transform.right * 1f);



                while (
                    Vector3.Distance(player.transform.position, targetPlayerPosition) > 0.01f &&
                    Vector3.Distance(camera.transform.position, targetCameraPosition) > 0.01f
                )
                {
                    float transformSpeed = 7.5f * Time.deltaTime;

                    player.transform.position =
                        Vector3.Lerp(player.transform.position, targetPlayerPosition, transformSpeed);


                    Quaternion playerRotation = player.transform.rotation;
                    player.transform.forward = OnlineTrigger.transform.forward;

                    player.transform.rotation =
                        Quaternion.Slerp(playerRotation, player.transform.rotation, transformSpeed);

                    camera.transform.position =
                        Vector3.Lerp(camera.transform.position, targetCameraPosition, transformSpeed);
                    Quaternion cameraRotation = camera.transform.rotation;
                    camera.transform.LookAt(targetPlayerPosition + (OnlineTrigger.transform.up * 1f) +
                                            (-OnlineTrigger.transform.right * 1f));

                    camera.transform.rotation =
                        Quaternion.Slerp(cameraRotation, camera.transform.rotation, transformSpeed);

                    yield return null;
                }

                player.transform.position = targetPlayerPosition;
                camera.transform.position = targetCameraPosition;
                OnlineMenu.gameObject.SetActive(true);
            }

            StartCoroutine(ChangeOnlineEnter());
        }

        public void CloseRoomMenu()
        {
            CloseRoom();
        }

        public void CloseRoom(Action OnComplete = null)
        {
            OnlineTrigger.GetComponentInChildren<Canvas>().enabled = true;

            var player = PlayerController.Instance;
            var camera = Camera.main;


            IEnumerator ChangeOutfitExit()
            {
                yield return null;

                Vector3 targetPlayerPosition =
                    OnlineTrigger.transform.position + (OnlineTrigger.transform.forward * 5f);



                while (
                    (Vector3.Distance(player.transform.position, targetPlayerPosition) > 0.01f &&
                    Vector3.Distance(camera.transform.position, targetPlayerPosition) > 0.01f) && player != null
                )
                {
                    float transformSpeed = 3f * Time.deltaTime;

                    player.transform.position =
                        Vector3.Lerp(player.transform.position, targetPlayerPosition, transformSpeed);

                    camera.transform.position =
                        Vector3.Lerp(camera.transform.position, targetPlayerPosition, transformSpeed);

                    camera.transform.forward =
                        Vector3.Lerp(camera.transform.forward, player.transform.forward, transformSpeed);
                }

                if (player)
                {
                    player.transform.position = targetPlayerPosition;

                    player.ShowModel = false;
                    player.CanMove = true;
                    player.UnlockCamera = false;
                }

                OnlineMenu.gameObject.SetActive(false);
                
                if (OnComplete != null)
                    OnComplete.Invoke();
            }

            StartCoroutine(ChangeOutfitExit());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (OnlineMenu.activeSelf)
                {
                    CloseRoom();
                }
                else if (OutfitMenu.activeSelf)
                {
                    CloseOutfit();
                }
            }

            if (RoomScreenMgr.gameObject.activeSelf != NetworkMgr.Instance.IsConnectedToRoom)
            {
                RoomScreenMgr.gameObject.SetActive(NetworkMgr.Instance.IsConnectedToRoom);
            }
        }
    }
}