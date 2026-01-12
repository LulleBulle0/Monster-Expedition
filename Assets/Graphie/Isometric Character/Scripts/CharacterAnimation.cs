using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Graphie.Assets.Characters
{
    public class CharacterAnimation : MonoBehaviour
    {
        [SerializeField] private Animator animator = null;
        [SerializeField] private List<string> stateNames = new List<string>();
        private int targetStateId = 0;
        private static float currentStateTime = 0;

        public string CurrentState { get; private set; }

        private void Reset()
        {
            animator = GetComponent<Animator>();
            stateNames = new List<string>
            {
                "Front_Idle_Stand",
                "Front_Walk",
                "Front_Sit",
                "Front_Idle_Sit",
                "Back_Idle_Stand",
                "Back_Walk",
                "Back_Sit",
                "Back_Idle_Sit"
            };
        }

        private void OnEnable()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
            animator.Play(stateNames[targetStateId], -1, currentStateTime);
        }

        private void Update()
        {
            currentStateTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        }

        public void SetAnimation(int stateId)
        {
            stateId = Mathf.Clamp(stateId, 0, stateNames.Count - 1);
            targetStateId = stateId;

            if (animator.gameObject.activeInHierarchy)
            {
                animator.CrossFadeInFixedTime(stateNames[stateId], .2f);
            }

            CurrentState = stateNames[targetStateId];
        }
    }
}