using System.Collections;
using System.Collections.Generic;
using Graphie.Assets.Characters;

using UnityEngine;
using UnityEngine.UI;

public class CharacterControls : MonoBehaviour
{
    [SerializeField]
    Text animationState;
    [SerializeField]
    private List<GameObject> characterSkins = new List<GameObject>();
    private int currentSkin = 0, currentAnimation = 0;
    [SerializeField]
    private int totalAnimationClips = 0;
    [SerializeField]
    private List<CharacterAnimation> characterAnimations = new List<CharacterAnimation>();
    private void Reset()
    {
        characterSkins = new List<GameObject>();
        characterAnimations = new List<CharacterAnimation>();
        foreach (Transform child in transform)
        {
            characterSkins.Add(child.gameObject);
            characterAnimations.Add(child.GetComponent<CharacterAnimation>());
            child.gameObject.SetActive(false);
        }

        if (characterSkins.Count > 0)
        {
            characterSkins[0].SetActive(true);
        }

        totalAnimationClips = 8;
        List<Renderer> allRenderers = new List<Renderer>();
        GetComponentsInChildren<Renderer>(true, allRenderers);
    }

    private void Update()
    {
        #region Skin
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            currentSkin++;
            SetSkin();
        }
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            currentSkin--;
            SetSkin();
        }
        #endregion

        #region Animation
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            currentAnimation++;
            SetAnimation();
        }
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            currentAnimation--;
            SetAnimation();
        }
        #endregion
    }

    private void Awake()
    {
        currentSkin = 0;
        SetSkin();

        currentAnimation = 0;
        SetAnimation();
    }

    private void SetSkin()
    {
        if (characterSkins.Count <= 0) return;
        currentSkin = (int)Mathf.Repeat(currentSkin, characterSkins.Count);
        foreach (GameObject skin in characterSkins)
        {
            if (skin.activeInHierarchy)
                skin.SetActive(false);
        }
        characterSkins[currentSkin].SetActive(true);
    }
    private void SetAnimation()
    {
        currentAnimation = (int)Mathf.Repeat(currentAnimation, totalAnimationClips);
        foreach (CharacterAnimation characterAnimation in characterAnimations)
        {
            characterAnimation.SetAnimation(currentAnimation);
            animationState.text = $"Animation State: {characterAnimation.CurrentState}";
        }
    }
}
