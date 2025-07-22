using AssetKits.ParticleImage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Responsive screen-fit board; gameplay, scoring, save/load, SFX, and level progression.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;


    [Header("Board Config (fallback if no Level Sequence)")]
    public int rows = 4;
    public int columns = 3;

    [Tooltip("Percent of screen dimension used as outer margin on EACH side. 0.05 = 5%.")]
    [Range(0f, 0.25f)] public float screenMarginPercent = 0.05f;

    [Tooltip("Gap between cards as % of card width. 0.08 = 8% gap.")]
    [Range(0f, 0.25f)] public float gapPercent = 0.08f;

    [Header("Level Sequence (optional)")]
    [Tooltip("Add row/column pairs here to define progressive levels. If empty, rows/columns above are reused every level.")]
    public List<Vector2Int> levelLayouts = new();
    public bool loopSequence = true;
    public int currentLevelIndex { get; private set; } = 0;

    [Header("Assets")]
    public Card cardPrefab;
    public Sprite backSprite;
    public List<Sprite> frontSprites;

    [Header("Gameplay Settings")]
    public float flipDuration = 0.3f;
    public float mismatchFlipBackDelay = 0.75f;
    public int baseMatchPoints = 10;
    public int mismatchPenalty = -2;
    public int comboIncrement = 1;
    public bool autoLoad = true;

    [Header("Audio")]
    public AudioClip flipClip;
    public AudioClip matchClip;
    public AudioClip mismatchClip;
    public AudioClip gameOverClip;

    [Header("Debug")]
    public bool clearSaveOnStart = false;

    [Header("Particle")]
    public ParticleImage confetiFullScreen;


    public bool InputAllowed { get; set; } = true;


    private readonly List<Card> cards = new();
    private readonly Queue<Card> comparisonQueue = new();
    private bool comparisonWorkerRunning;

    public int score { get; private set; }
    private int combo;
    private bool gameFinished;
    public static bool hasSaveData;


    // Prefab design size (unscaled)
    private float prefabDesignW = 1f;
    private float prefabDesignH = 1f;

    public static Action LevelCompleteAction;
    public static Action<int, int> ScoreUpdateAction;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;


        CachePrefabDesignSize();


        if (clearSaveOnStart) SaveSystem.Clear();

        if (autoLoad && SaveSystem.HasSave())
        {
            if (!gameFinished)
            {
                hasSaveData = true;
            }
        }

    }


    #region Level Management
    private Vector2Int GetLayoutForLevel(int levelIdx)
    {
        if (levelLayouts != null && levelLayouts.Count > 0)
        {
            levelIdx = Mathf.Clamp(levelIdx, 0, levelLayouts.Count - 1);
            return levelLayouts[levelIdx];
        }
        return new Vector2Int(rows, columns);
    }

    public void StartNewGame()
    {
        StartLevel(0);
    }


    public void ContinueGame()
    {
        LoadGame();
        ScoreUpdateAction?.Invoke(score, combo);
    }





    private void StartLevel(int levelIdx)
    {
        currentLevelIndex = Mathf.Max(0, levelIdx);
        Vector2Int layout = GetLayoutForLevel(currentLevelIndex);

        PrepareLevelSprites();
        NewGame(layout.x, layout.y);
    }
    private void AdvanceLevel()
    {
        int next = currentLevelIndex;
        if (levelLayouts != null && levelLayouts.Count > 0)
        {
            if (next >= levelLayouts.Count)
                next = loopSequence ? 0 : levelLayouts.Count - 1;
        }
        else
        {

            next = 0;
        }

        StartLevel(next);
    }

    private IEnumerator LevelCompleteRoutine()
    {
        InputAllowed = false;
        PlayGameOverSfx();
        yield return new WaitForSeconds(1.0f);
        ClearBoard();
        currentLevelIndex++;
        SaveGameData();
        LevelCompleteAction?.Invoke();
    }

    private void PlayNextLevel()
    {
        confetiFullScreen.Stop();
        InputAllowed = true;
        AdvanceLevel();

    }

    #endregion

    public void NewGame(int r, int c)
    {
        rows = Mathf.Max(1, r);
        columns = Mathf.Max(1, c);

        ClearBoard();

        int total = rows * columns;
        if (total % 2 != 0)
        {
            Debug.LogWarning("Total cards must be even. Adjusting columns.");
            columns += 1;
            total = rows * columns;
        }

        int pairCount = total / 2;
        if (frontSprites.Count < pairCount)
        {
            Debug.LogError("Not enough front sprites assigned!");
            return;
        }

        // Build shuffled ID list
        List<int> ids = new(pairCount * 2);
        for (int i = 0; i < pairCount; i++) { ids.Add(i); ids.Add(i); }
        for (int i = 0; i < ids.Count; i++)
        {
            int swap = UnityEngine.Random.Range(i, ids.Count);
            (ids[i], ids[swap]) = (ids[swap], ids[i]);
        }

        CreateAndPlaceCards(ids);

        score = 0;
        combo = 0;
        gameFinished = false;
        SaveGameData();
    }


    private void CreateAndPlaceCards(List<int> ids)
    {
        Camera cam = Camera.main;
        if (!cam)
        {
            Debug.LogError("GameManager: No MainCamera found.");
            return;
        }


        float worldH = 2f * cam.orthographicSize;
        float worldW = worldH * cam.aspect;


        float margin = Mathf.Clamp01(screenMarginPercent);
        float usableW = worldW * (1f - margin * 2f);
        float usableH = worldH * (1f - margin * 2f);

        float aspect = prefabDesignH / prefabDesignW;


        float gapFrac = Mathf.Clamp01(gapPercent);
        float denomW = columns + (columns - 1) * gapFrac;
        float cardW_fromWidth = usableW / denomW;


        float denomH = (rows + (rows - 1) * gapFrac) * aspect;
        float cardW_fromHeight = usableH / denomH;


        float cardW = Mathf.Min(cardW_fromWidth, cardW_fromHeight);
        float cardH = cardW * aspect;
        float gap = cardW * gapFrac;


        float totalW = columns * cardW + (columns - 1) * gap;
        float totalH = rows * cardH + (rows - 1) * gap;


        Vector2 start = new(
            -totalW * 0.5f + cardW * 0.5f,
            totalH * 0.5f - cardH * 0.5f
        );


        float scaleFactor = (cardW / prefabDesignW) / 1.5f;


        for (int i = 0; i < ids.Count; i++)
        {
            int row = i / columns;
            int colIdx = i % columns;

            Card card = Instantiate(cardPrefab, transform);


            card.transform.position = new Vector3(
                start.x + colIdx * (cardW + gap),
                start.y - row * (cardH + gap),
                0f
            );
            card.transform.localScale = Vector3.one * scaleFactor;


            int id = ids[i];
            card.Init(id, frontSprites[id], backSprite, this, scaleFactor);

            cards.Add(card);
        }
    }

    private void CachePrefabDesignSize()
    {
        prefabDesignW = 1f;
        prefabDesignH = 1f;

        if (!cardPrefab) return;

        // Prefer collider
        BoxCollider2D col = cardPrefab.GetComponent<BoxCollider2D>();
        if (col)
        {
            prefabDesignW = Mathf.Abs(col.size.x);
            prefabDesignH = Mathf.Abs(col.size.y);
            if (prefabDesignW > 0f && prefabDesignH > 0f) return;
        }

        // Fallback sprite
        var sr = cardPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr && sr.sprite)
        {
            prefabDesignW = Mathf.Abs(sr.sprite.bounds.size.x);
            prefabDesignH = Mathf.Abs(sr.sprite.bounds.size.y);
        }

        if (prefabDesignW <= 0f) prefabDesignW = 1f;
        if (prefabDesignH <= 0f) prefabDesignH = prefabDesignW;
    }

    private void PrepareLevelSprites()
    {
        // Shuffle frontSprites so new level uses a new random order of sprites
        for (int i = frontSprites.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            (frontSprites[i], frontSprites[randomIndex]) = (frontSprites[randomIndex], frontSprites[i]);
        }
    }
    private void ClearBoard()
    {
        foreach (var c in cards)
            if (c) Destroy(c.gameObject);

        cards.Clear();
        comparisonQueue.Clear();
        comparisonWorkerRunning = false;
    }

    public void NotifyCardRevealed(Card card)
    {
        comparisonQueue.Enqueue(card);
        TryStartComparisonWorker();
    }

    private void TryStartComparisonWorker()
    {
        if (!comparisonWorkerRunning)
            StartCoroutine(ComparisonWorker());
    }

    private IEnumerator ComparisonWorker()
    {
        comparisonWorkerRunning = true;
        while (comparisonQueue.Count >= 2)
        {
            Card first = DequeueNextActive();
            Card second = DequeueNextActive();
            if (first == null || second == null)
                break;

            if (first.CardID == second.CardID)
            {
                first.MarkMatched();
                second.MarkMatched();
                combo++;
                int comboMultiplier = 1 + (combo - 1) * comboIncrement;
                AddScore(baseMatchPoints * comboMultiplier);
                PlayMatchSfx();
            }
            else
            {
                StartCoroutine(DelayedFlipBack(first, second));
                combo = 0;
                AddScore(mismatchPenalty);
                PlayMismatchSfx();
            }

            yield return null;
        }

        comparisonWorkerRunning = false;

        // Level cleared?
        if (!gameFinished && cards.All(c => c.IsMatched))
        {
            gameFinished = true;
            confetiFullScreen.Play();
            StartCoroutine(LevelCompleteRoutine());


        }
    }




    private Card DequeueNextActive()
    {
        while (comparisonQueue.Count > 0)
        {
            var c = comparisonQueue.Dequeue();
            if (c != null && c.IsFlipped && !c.IsMatched)
                return c;
        }
        return null;
    }

    private IEnumerator DelayedFlipBack(Card a, Card b)
    {
        yield return new WaitForSeconds(mismatchFlipBackDelay);
        a.ForceFlipBack();
        b.ForceFlipBack();
        SaveGameData();
    }

    private void AddScore(int delta)
    {
        score += delta;
        if (score < 0) score = 0;
        SaveGameData();
        Debug.Log($"Score: {score}  Combo: {combo}");
        ScoreUpdateAction?.Invoke(score, combo);
    }

    #region Audio Helpers
    public void PlayFlipSfx() => AudioManager.Instance?.Play(flipClip);
    private void PlayMatchSfx() => AudioManager.Instance?.Play(matchClip);
    private void PlayMismatchSfx() => AudioManager.Instance?.Play(mismatchClip);
    private void PlayGameOverSfx() => AudioManager.Instance?.Play(gameOverClip);
    #endregion

    #region Save / Load
    private void SaveGameData()
    {
        SaveData data = new()
        {
            rows = rows,
            cols = columns,
            score = score,
            combo = combo,
            levelIndex = currentLevelIndex,
            cardIDs = cards.Select(c => c.CardID).ToArray(),
            matched = cards.Select(c => c.IsMatched).ToArray()
        };
        SaveSystem.Save(data);
    }

    private void LoadGame()
    {
        var data = SaveSystem.Load();
        if (data == null)
        {
            Debug.LogError("The Datawas null");
            StartLevel(0);
            return;
        }

        currentLevelIndex = data.levelIndex;
        rows = data.rows;
        columns = data.cols;
        ClearBoard();
        if (data.cardIDs.Length > 0)
        {
            CreateAndPlaceCards(data.cardIDs.ToList());
        }
        else
        {
            StartLevel(currentLevelIndex);
        }

        score = data.score;
        combo = data.combo;

        // restore matched states
        for (int i = 0; i < cards.Count && i < data.matched.Length; i++)
        {
            if (data.matched[i])
            {
                var card = cards[i];
                card.RevealInstant(frontSprites[card.CardID]);
                card.MarkMatched();
            }
        }
        gameFinished = cards.All(c => c.IsMatched);
        Debug.Log($"Loaded game. Score {score}, Combo {combo}, Level {currentLevelIndex}");
    }
    #endregion

    private void OnEnable()
    {
        UiManager.NewGameAction += StartNewGame;
        UiManager.ContinueGameAction += ContinueGame;
        UiManager.NextLevelAction += PlayNextLevel;
        UiManager.ClearBoardAction += ClearBoard;
    }

    private void OnDisable()
    {
        UiManager.NewGameAction -= StartNewGame;
        UiManager.ContinueGameAction -= ContinueGame;
        UiManager.NextLevelAction -= PlayNextLevel;
        UiManager.ClearBoardAction -= ClearBoard;
    }




    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            SaveGameData();
        }
    }

    public void OnApplicationQuit()
    {
        SaveGameData();
    }

}

[Serializable]
public class SaveData
{
    public int rows;
    public int cols;
    public int score;
    public int combo;
    public int levelIndex;
    public int[] cardIDs;
    public bool[] matched;
}
