using System;
using UnityEngine;
using UnityEngine.Events;

namespace DefaultNamespace
{
    public class TriggerController : MonoBehaviour
    {
        public UnityEvent OnEnter;
        public UnityEvent OnStay;
        public UnityEvent OnExit;

        private void OnTriggerEnter(Collider other)
        {
            OnEnter.Invoke();
        }

        private void OnTriggerStay(Collider other)
        {
            OnStay.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            OnExit.Invoke();
        }
    }
}