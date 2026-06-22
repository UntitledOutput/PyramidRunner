using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Utils
{
    public static class InputSystem
    {
        public static PlayerControls Controls;
    
        public enum DeviceType
        {
            KeyboardMouse,
            Xbox,
            PlayStation,
            Switch,
            GenericGamepad,
            Touch,
            Unknown
        }

        private static Dictionary<Type, DeviceType> _baseDeviceMap = new()
        {
            { typeof(Keyboard), DeviceType.KeyboardMouse },
            { typeof(Mouse), DeviceType.KeyboardMouse },
            { typeof(Gamepad), DeviceType.GenericGamepad },
        };

        
        private static InputUser _user;
        private static InputDevice _lastDevice;

        public static DeviceType CurrentDeviceType;
        public static UnityEvent OnDeviceChanged;
    
        static InputSystem()
        {
            Controls = new PlayerControls();
            Controls.Enable();

        }
    
        public static Vector2 Move => Controls.Player.Move.ReadValue<Vector2>();
        public static Vector2 Look => Controls.Player.Look.ReadValue<Vector2>();

        public static bool Attack => Controls.Player.Attack.IsPressed() && !EventSystem.current.IsPointerOverGameObject();
        public static bool Jump => Controls.Player.Jump.IsPressed();
        public static bool Interact => Controls.Player.Interact.WasPressedThisFrame();
        public static bool Crouch => Controls.Player.Crouch.IsPressed();
        public static bool Sprint => Controls.Player.Sprint.IsPressed();



    }
}
