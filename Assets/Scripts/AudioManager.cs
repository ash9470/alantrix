using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    [SerializeField] internal AudioSource oneShotSource;

  
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
      

        oneShotSource.playOnAwake = false;
    }



    public void Play(AudioClip clip)
    {
        if (clip != null) oneShotSource.PlayOneShot(clip);
    }
}
