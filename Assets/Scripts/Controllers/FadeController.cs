using System;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class FadeController : MonoBehaviour
    {
        public static FadeController Instance;
        [Range(0,1)] public float FadeAmount = 0f;

        public Image FadeImage;
        public float FadeSpeed = 3f;
        
        private void Awake()
        {
            if (Instance)
            {
                DestroyImmediate(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            Instance = this;
        }

        private void Update()
        {
            
            
            FadeImage.color = new Color(FadeImage.color.r, FadeImage.color.g, FadeImage.color.b, Mathf.Lerp(FadeImage.color.a, FadeAmount, Time.deltaTime*FadeSpeed));
        }
    }
}