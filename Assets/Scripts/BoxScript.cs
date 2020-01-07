﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Database;
using TMPro;
using EZCameraShake;


/**
 * Design TODOs:
 *
 * Potential components:
 * 1. "Physics" component (tiles falling)
 * 2. Individual box/letters component (basic info like grid location, letter, etc)
 * 3. "Word"/scoring component (current word selections, scoring functions, etc.)
 * 4. Animation/Visual component (particle systems, highlight colors, score/text displays, etc.)
 * 5. Audio component
 * 6. Logging component (maybe need to create a separate class for this that handles all the logging??)
 */

public class BoxScript : MonoBehaviour
{
    static BoxScript instance;   // static reference to itself

    public static float gridWidthRadius = 2.5f;
    public static int gridHeightRadius = 4;
    public static int gridWidth = (int)(gridWidthRadius * 2) + 1; // -2.5 to 2.5 
    public static int gridHeight = gridHeightRadius * 2 + 1; // -4 to 4
    public static Transform[,] grid = new Transform[gridWidth, gridHeight];
    public static List<Vector2> currentSelection = new List<Vector2>();
    public static Text scoreText;
    public static Text selectedScore;
    public static long score;
    private const float FALL_SPEED_CONST = 0.025f;
    public static string currentWord = "";
    public static Dictionary<string, Vector2> freqDictionary;
    public static int totalInteractions;
    public static int wordsPlayed;
    public GameObject _explodeParticleSystemPrefab;
    public GameObject _bubblesLandingParticleSystemPrefab;
    public GameObject _shinyParticleSystemPrefab;
    public static GameObject celebrationParticleSystem;
    public static Color originalBlockColor;

    static float[] freqCutoffs = {
            .4f, .3f,
            .24f, .22f,
            .2f, .15f,
            .1f
        };
    public string Letter { get; set; }
    float fall;
    bool falling = true;
    bool columnFalling;
    int myX;
    int myY;
    bool needsToLand = true;
    bool isSelected;

    [NonSerialized]
    public float fallSpeed = FALL_SPEED_CONST;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        // initialize objects
        if (this.gameObject.transform.GetChild(0).GetComponent<TextMeshPro>().text.Length > 0)
        {
            Letter = this.gameObject.transform.GetChild(0).GetComponent<TextMeshPro>().text;
        }

        if (scoreText == null)
        {
            scoreText = GameObject.Find("Score").GetComponent<Text>();
        }

        if (selectedScore == null)
        {
            selectedScore = GameObject.Find("SelectedScore").GetComponent<Text>();
        }

        if (celebrationParticleSystem == null)
        {
            celebrationParticleSystem = GameObject.Find("CelebrationParticles");
        }

        originalBlockColor = new Color(9f / 255f, 132f / 255f, 227f / 255f, 202f / 255f);
    }

    // Use this for initialization
    void Start()
    {
        // Add the location of the block to the grid
        Vector2 v = transform.position;
        myX = (int)(v.x + gridWidthRadius);
        myY = (int)v.y + gridHeightRadius;
        grid[myX, myY] = transform;
        falling = true;
        columnFalling = false;
        fall = Time.time;

        // update the progress bar to fill in how many turns are left
        //UpdateScoreProgressBar();

        if (!IsValidPosition())
        {
            //SceneManager.LoadScene (0);
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // check to see if the column needs to go down, or if it needs to be refilled
        if (!falling && myY > 0 && grid[myX, myY - 1] == null && Time.time - fall >= fallSpeed)
        {
            if (!IsOtherBoxInColumnFalling())
            {
                needsToLand = true;
                ColumnDown();
                fall = Time.time;
            }
        }
        else if (columnFalling && ((myY > 0 && grid[myX, myY - 1] != null) || myY == 0))
        {
            columnFalling = false;
        }

        // If a tile is falling down the screen...
        if (falling && Time.time - fall >= fallSpeed)
        {
            needsToLand = true;
            transform.position += new Vector3(0, -1, 0);

            if (IsValidPosition())
            {
                GridUpdate();
            }
            else
            {
                transform.position += new Vector3(0, 1, 0);
                falling = false;
                fallSpeed = FALL_SPEED_CONST;
            }
            fall = Time.time;
        }

        // check to see if landing bubbles need to be activated
        if (!falling && needsToLand && !columnFalling)
        {
            OnLand();
            needsToLand = false;
        }
    }

    public IEnumerator AnimateBouncingTile()
    {
        float z = 0f;
        int direction = 1;
        float maxRotation = 10f;
        while (isSelected)
        {
            z += UnityEngine.Random.Range(1f, 5f) * direction;

            // flip direction if it reaches max
            if (maxRotation < Mathf.Abs(z))
            {
                z = maxRotation * direction;
                direction *= -1;
            }

            transform.rotation = Quaternion.Euler(0, 0, z);
            yield return new WaitForSeconds(0.001f);
        }

        // reset tile to original rotation
        transform.rotation = Quaternion.Euler(0, 0, 0);

        // end animation
        yield return null;
    }

    // things that happen when the box lands
    public void OnLand()
    {
        // ======FEATURE:  Juice, extra landing animation======
        if (GameManagerScript.JUICE_UNPRODUCTIVE || GameManagerScript.JUICE_PRODUCTIVE)
        {
            CreateBubbleParticles();
        }

        AudioManager.instance.Play("Select");
    }

    public void CreateBubbleParticles()
    {
        //Instantiate _particleSystemPrefab as new GameObject.
        GameObject _particleSystemObj = Instantiate(_bubblesLandingParticleSystemPrefab);

        // Set new Particle System GameObject as a child of desired GO.
        // Right now parent would be the same GO in which this script is attached
        // You can also make it others child by ps.transform.parent = otherGO.transform.parent;

        // After setting this, replace the position of that GameObject as where the parent is located.
        Vector3 pos = transform.position;
        pos.y -= .25f;
        _particleSystemObj.transform.position = pos;

        ParticleSystem _particleSystem = _particleSystemObj.GetComponent<ParticleSystem>();
        _particleSystem.Play();
    }

    public void SetLetter(char letter)
    {
        Letter = letter + "";
        this.gameObject.transform.GetChild(0).GetComponent<TextMeshPro>().text = Letter;
    }

    public static LogEntry.LetterPayload[] GetLetterPayloadsFromCurrentWord()
    {
        LogEntry.LetterPayload[] toRet = new LogEntry.LetterPayload[currentSelection.Count];
        for (int i = 0; i < currentSelection.Count; ++i)
        {
            toRet[i] = new LogEntry.LetterPayload(currentWord[i] + "", (int)currentSelection[i].x, (int)currentSelection[i].y);
        }

        return toRet;
    }

    public static void PlayWord()
    {
        SubmitWordLogEntry dbEntry = new SubmitWordLogEntry();
        dbEntry.parentKey = "BNW_Action";
        dbEntry.key = "BNW_Submit";
        bool valid = UpdateScore(dbEntry);

        // Firebase logging
        if (GameManagerScript.LOGGING)
        {
            //Debug.Log("Attempts to log data");
            Logger.LogKeyFrame("pre");

            string json = JsonUtility.ToJson(dbEntry);
            DatabaseReference reference = FirebaseDatabase.DefaultInstance.GetReference(GameManagerScript.LOGGING_VERSION);
            DatabaseReference child = reference.Push();
            child.SetRawJsonValueAsync(json);
            ++totalInteractions;
        }

        //=========Screen animations based on if word was valid or not==========

        // shake the camera
        CameraShaker.Instance.ShakeOnce(5f, 5f, .1f, .6f);


        if (valid)
        {
            // temporarily turn off input until all boxes fall!
            TouchInputHandler.inputEnabled = false;

            // keep track of how many words have been successfully played
            ++wordsPlayed;

            // ANIMATE JUICY SUCCESS AND SOUNDS!!
            if (GameManagerScript.JUICE_PRODUCTIVE || GameManagerScript.JUICE_UNPRODUCTIVE)
            {
                ParticleSystem _particleSystem = celebrationParticleSystem.GetComponent<ParticleSystem>();
                _particleSystem.Play();

                AudioManager.instance.Play("Explosion");
            }
            else
            {
                // default sound effect
                AudioManager.instance.Play("WaterSwirl");
            }
        }
        else
        {
            // Error sound
            AudioManager.instance.Play("Error");

            // JUICY SOUND FX
            if (GameManagerScript.JUICE_PRODUCTIVE || GameManagerScript.JUICE_UNPRODUCTIVE)
            {
                AudioManager.instance.Play("Explosion");
            }
        }
    }

    public static bool UpdateScore(SubmitWordLogEntry dbEntry)
    {
        // firebase logging
        SubmitWordLogEntry.SubmitWordPayload payload = new SubmitWordLogEntry.SubmitWordPayload();
        payload.word = currentWord;
        payload.letters = GetLetterPayloadsFromCurrentWord();
        dbEntry.payload = payload;

        if (IsValidWord(currentWord))
        {
            // Update the score based on the word
            long submittedScore = GetScore(currentWord, payload);
            score += submittedScore;
            if (scoreText != null) { scoreText.text = "Points: " + score; }

            payload.success = true;
            payload.scoreTotal = submittedScore;

            // update the highest scoring word if necessary
            if (submittedScore > GameManagerScript.myHighestScoringWordScore)
            {
                GameManagerScript.myHighestScoringWord = currentWord;
                GameManagerScript.myHighestScoringWordScore = (int)submittedScore;
            }

            if (score > GameManagerScript.myHighScore)
            {
                GameManagerScript.myHighScore = score;
                GameManagerScript.myHighScoreUpdated = true;
            }

            // Do something celebratory! highlight in green briefly before removing from screen
            // and also display a congratulatory message depending on how rare the word was
            instance.StartCoroutine(instance.AnimateSelectedTiles(GetWordFreq(currentWord), submittedScore));

            // Update the high score, if applicable
            // TODO: debug this
            //DBManager.instance.LogScore(score);

            return true;
        }
        else
        {
            // firebase logging
            payload.success = false;
            payload.rarity = -1;
            payload.scoreBase = -1;
            payload.scoreTotal = -1;

            ClearAllSelectedTiles();

            return false;
        }
    }

    public static long GetScore(string word, SubmitWordLogEntry.SubmitWordPayload payload)
    {
        // base score is based on length of word (if word is 3 letters long, base is 1 + 2 + 3 points, etc)
        long baseScore = CalculateBaseScore(word.Length);

        // TODO: do more balance testing of scoring function to make sure it is balanced?
        float wordRank = GetWordRank(word);

        if (wordRank < 0)
        {
            // word does not exist in dictionary
            return 0;
        }

        // update the rarest word if necessary
        if (wordRank > GameManagerScript.myRarestWordRarity)
        {
            GameManagerScript.myRarestWord = currentWord;
            GameManagerScript.myRarestWordRarity = wordRank;
        }

        // record scores and freq into log if necessary
        if (payload != null)
        {
            payload.rarity = wordRank;
            payload.scoreBase = baseScore;
        }

        // scoring function based on freq of word + freq of letters

        // based on the rarity of the word, Uncommon, Rare, Super Rare, Ultra Rare?
        // give the user a fixed bonus points amount and display an animation
        int bonus = GetBonus(wordRank) / 2;

        return (long)(baseScore * (1 + wordRank * 4)) + bonus;
    }

    static int GetBonus(float freq)
    {
        if (freq >= freqCutoffs[0])
        {
            // PREMIUM ULTRA RARE
            return 75; // over 9000
        }
        else if (freq >= freqCutoffs[1])
        {
            // PREMIUM ULTRA RARE
            return 60;
        }
        else if (freq >= freqCutoffs[2])
        {
            // ULTRA RARE+
            return 50;
        }
        else if (freq >= freqCutoffs[3])
        {
            // ULTRA RARE
            return 40;
        }
        else if (freq >= freqCutoffs[4])
        {
            // SUPER RARE
            return 30;
        }
        else if (freq >= freqCutoffs[5])
        {
            // RARE
            return 20;
        }
        else if (freq >= freqCutoffs[6])
        {
            // AVERAGE
            return 10;
        }

        return 0;
    }

    public IEnumerator AnimateSelectedTiles(float freq, long score)
    {
        // animate different congratulatory messages based on score
        TextFaderScript textFader = GameObject.Find("SuccessMessage").GetComponent<TextFaderScript>();
        TextFaderScript scoreTextFader = GameObject.Find("SuccessScorePanel").GetComponent<TextFaderScript>();

        if (freq >= freqCutoffs[0])
        {
            // Unbelievable
            textFader.FadeText(0.7f, "Unbelievable!");
        }
        else if (freq >= freqCutoffs[1])
        {
            // Ultra rare
            textFader.FadeText(0.7f, "Ultra Rare!");
        }
        else if (freq >= freqCutoffs[2])
        {
            // Super rare
            textFader.FadeText(0.7f, "Super Rare!");

        }
        else if (freq >= freqCutoffs[3])
        {
            // Rare
            textFader.FadeText(0.7f, "Rare!");
        }
        else if (freq >= freqCutoffs[4])
        {
            // Great
            textFader.FadeText(0.7f, "Great!");
        }
        else if (freq >= freqCutoffs[5])
        {
            // Good
            textFader.FadeText(0.7f, "Good!");
        }

        // Animate the obtained score
        scoreTextFader.FadeText(0.7f, "+" + score + " points");

        // animate each selected tile
        foreach (Vector2 v in currentSelection)
        {
            GameObject _gameObject = grid[(int)v.x, (int)v.y].gameObject;
            _gameObject.GetComponent<BoxScript>().AnimateSuccess();
        }

        // brief pause for the color to change before removing them from screen
        yield return new WaitForSeconds(.4f);

        // Delete all tiles
        DeleteAllSelectedTiles();
    }

    static long CalculateBaseScore(int length)
    {
        if (length == 0)
        {
            return 0;
        }

        return LengthScoreFunction(length) + CalculateBaseScore(length - 1);
    }

    static long LengthScoreFunction(int length)
    {
        if (length == 1)
        {
            return 1;
        }

        return 1 + LengthScoreFunction(length - 1);
    }

    public void ResetTileColors()
    {
        gameObject.transform.Find("Block_bg").GetComponent<SpriteRenderer>().color = originalBlockColor;
        gameObject.transform.Find("Border").GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0f);
    }

    public static void RemoveTilesPastIndex(int index)
    {
        if (index >= currentSelection.Count)
        {
            //Debug.Log("Error: tried to remove tiles past index out of bounds.");
            return;
        }

        for (int i = currentSelection.Count - 1; i > index; --i)
        {
            // get the last selected letter tile and remove it from the list (and unhighlight it)
            Vector2 v = currentSelection[currentSelection.Count - 1];
            BoxScript box = grid[(int)v.x, (int)v.y].gameObject.GetComponent<BoxScript>();
            box.ResetTileColors();
            box.isSelected = false;
            currentSelection.Remove(v);

            /******************************************************************
             * FEATURE: If juiciness is on, play particles when deselecting!
             ******************************************************************/
            if (GameManagerScript.JUICE_PRODUCTIVE || GameManagerScript.JUICE_UNPRODUCTIVE)
            {
                box.PlayShinySelectParticles();
            }
        }

        // log the removed letters
        Logger.LogAction("BNW_LetterDeselected", currentWord.Substring(index + 1), -1, -1);

        // Remove the letters
        currentWord = currentWord.Substring(0, index + 1);

        /*****************************************************************
         * FEATURE: Highlighting color gradient based on frequency feature
         *****************************************************************/
        DisplayHighlightFeedback();

        /******************************************************************
         * FEATURE: Display currently selected score
         ******************************************************************/
        DisplaySelectedScore();

        /******************************************************************
         * FEATURE: Obstructions- enable/disable play button based on word
         ******************************************************************/
        if (GameManagerScript.OBSTRUCTION_PRODUCTIVE || GameManagerScript.OBSTRUCTION_UNPRODUCTIVE)
        {
            GameManagerScript.gameManager.UpdatePlayButton();
        }

        // Play sound effect
        AudioManager.instance.Play("Select");
    }

    public static void RemoveLastSelection()
    {
        /******************************************************************
         * FEATURE: If juiciness is on, play particles when deselecting!
         ******************************************************************/
        if (GameManagerScript.JUICE_PRODUCTIVE || GameManagerScript.JUICE_UNPRODUCTIVE)
        {
            Vector2 last = currentSelection[currentSelection.Count - 1];
            grid[(int)last.x, (int)last.y].gameObject.GetComponent<BoxScript>().PlayShinySelectParticles();
        }

        // get the last selected letter tile and remove it from the list (and unhighlight it)
        Vector2 v = currentSelection[currentSelection.Count - 1];
        BoxScript box = grid[(int)v.x, (int)v.y].gameObject.GetComponent<BoxScript>();
        box.ResetTileColors();
        box.isSelected = false;
        currentSelection.Remove(v);

        // log the last removed letter
        Logger.LogAction("BNW_LetterDeselected", currentWord.Substring(currentWord.Length - 1, 1), (int)v.x, (int)v.y);

        // Remove the last letter
        currentWord = currentWord.Substring(0, currentWord.Length - 1);

        /*****************************************************************
         * FEATURE: Highlighting color gradient based on frequency feature
         *****************************************************************/
        DisplayHighlightFeedback();

        /******************************************************************
         * FEATURE: Display currently selected score
         ******************************************************************/
        DisplaySelectedScore();

        /******************************************************************
         * FEATURE: Obstructions- enable/disable play button based on word
         ******************************************************************/
        if (GameManagerScript.OBSTRUCTION_PRODUCTIVE || GameManagerScript.OBSTRUCTION_UNPRODUCTIVE)
        {
            GameManagerScript.gameManager.UpdatePlayButton();
        }

        // Play sound effect
        AudioManager.instance.Play("Select");
    }

    public bool IsInsideTile(Vector2 pos, float sensitivity)
    {
        Vector2 realPos = Camera.main.ScreenToWorldPoint(pos);
        Vector2 myRealPos = new Vector2(myX - gridWidthRadius, myY - gridHeightRadius);
        float radius = 0.55f * sensitivity;

        // calculate distance between the two points and see if it's within the radius
        return Vector2.Distance(realPos, myRealPos) <= radius;
    }

    // Checks to see if there is another box in my column that is falling with me in a 'column fall'
    bool IsOtherBoxInColumnFalling()
    {
        for (int y = myY - 1; y >= 0; --y)
        {
            if (grid[myX, y] != null && grid[myX, y].gameObject.GetComponent<BoxScript>().columnFalling)
            {
                return true;
            }
        }

        return false;
    }

    // Checks to see if there are any boxes in column x that is currently falling (or column falling)
    public static bool IsBoxInColumnFalling(int x)
    {
        for (int y = 0; y < gridHeight; ++y)
        {
            if (grid[x, y] != null && (grid[x, y].gameObject.GetComponent<BoxScript>().falling ||
                                        grid[x, y].gameObject.GetComponent<BoxScript>().columnFalling))
            {
                return true;
            }
        }

        return false;
    }

    public static void DeleteAllSelectedTiles()
    {
        //Debug.Log("deleting all tiles");
        // delete all tiles in list
        foreach (Vector2 v in currentSelection)
        {
            GameObject gameObject = grid[(int)v.x, (int)v.y].gameObject;
            Destroy(gameObject);
            grid[(int)v.x, (int)v.y] = null;
        }
        currentWord = "";
        currentSelection.Clear();

        /******************************************************************
         * FEATURE: Display currently selected score
         ******************************************************************/
        selectedScore.text = "";

        /******************************************************************
         * FEATURE: Obstructions- enable/disable play button based on word
         ******************************************************************/
        if (GameManagerScript.OBSTRUCTION_PRODUCTIVE || GameManagerScript.OBSTRUCTION_UNPRODUCTIVE)
        {
            GameManagerScript.gameManager.UpdatePlayButton();
        }
    }

    public void AnimateSuccess()
    {
        //Debug.Log("Animating success...");

        // Play the particle system
        PlaySuccessParticleSystem();

        // hide ALL the sprites
        foreach (Transform child in gameObject.transform)
        {
            SpriteRenderer sp = child.GetComponent<SpriteRenderer>();
            if (sp != null)
            {
                sp.enabled = false;
            }
        }

        gameObject.GetComponent<SpriteRenderer>().enabled = false;

        // hide the textmesh pro
        gameObject.GetComponentInChildren<TextMeshPro>().enabled = false;
    }

    public void PlayParticleSystem(GameObject particleSystemPrefab)
    {
        //Instantiate _particleSystemPrefab as new GameObject.
        GameObject _particleSystemObj = Instantiate(particleSystemPrefab);

        // Set new Particle System GameObject as a child of desired GO.
        // Right now parent would be the same GO in which this script is attached
        // You can also make it others child by ps.transform.parent = otherGO.transform.parent;

        // After setting this, replace the position of that GameObject as where the parent is located.
        _particleSystemObj.transform.position = transform.position;

        ParticleSystem _particleSystem = _particleSystemObj.GetComponent<ParticleSystem>();
        _particleSystem.Play();
    }

    public void PlaySuccessParticleSystem()
    {
        PlayParticleSystem(_explodeParticleSystemPrefab);
    }

    public static bool IsValidWord(string word)
    {
        if (word.Length < 3)
        {
            // Error message
            TextFaderScript textFader = GameObject.Find("SuccessMessage").GetComponent<TextFaderScript>();
            textFader.FadeErrorText(0.8f, "Word must be at least 3 letters");
        }

        return word.Length >= 3 && freqDictionary.ContainsKey(word);
    }

    public static float GetWordFreq(string word)
    {
        if (freqDictionary.ContainsKey(word))
        {
            return freqDictionary[word].x;
        }

        return -1;
    }

    public static float GetWordRank(string word)
    {
        if (freqDictionary.ContainsKey(word))
        {
            return freqDictionary[word].y;
        }

        return -1;
    }

    /*****************************************************************
     * FEATURE: Highlighting color gradient based on frequency feature
     *****************************************************************/
    public static Color IntToColor(int HexVal)
    {
        byte R = (byte)((HexVal >> 16) & 0xFF);
        byte G = (byte)((HexVal >> 8) & 0xFF);
        byte B = (byte)((HexVal) & 0xFF);

        return new Color(R / 255f, G / 255f, B / 255f, 1);
    }

    public static Color GetColorGradient(float rank)
    {
        // Uses a perceptually uniform color scale using the Hsluv library
        float max = 0.55f;
        float scale = rank / max;
        float hue = scale * 90 + 100;

        IList<double> rgb = Hsluv.HsluvConverter.HsluvToRgb(new double[] { hue, 100, 70 });

        return new Color((float)rgb[0],
                         (float)rgb[1],
                         (float)rgb[2],
                         240f / 255f);
    }

    /**
     * Returns a gradient of the highlighted letters color 
     */
    public static Color GetHighlightColor(string word)
    {
        Color highlightColor;

        if (word.Length >= 3 && freqDictionary.ContainsKey(word))
        {
            float rank = freqDictionary[word].y;

            // increase the color gradient by inverse gaussian as the freq increases linearly
            return GetColorGradient(rank);
        }
        else
        {
            if (ColorUtility.TryParseHtmlString("#c0392b", out highlightColor))
                return highlightColor;

            return Color.red;
        }
    }


    public void DisplayBorder()
    {
        gameObject.transform
            .Find("Border")
            .GetComponent<SpriteRenderer>()
            .color = Color.white;
    }

    public void PlayShinySelectParticles()
    {
        PlayParticleSystem(_shinyParticleSystemPrefab);
    }

    /**
     * Method that selects the tile at the given coordinate (pos)
     */
    public static void SelectTile(Vector2 pos)
    {
        currentSelection.Add(pos);
        GameObject gameObject = grid[(int)pos.x, (int)pos.y].gameObject;
        BoxScript boxScript = gameObject.GetComponent<BoxScript>();
        currentWord += boxScript.Letter;

        /*****************************************************************
         * FEATURE: Highlighting color gradient based on frequency feature
         *****************************************************************/
        DisplayHighlightFeedback();

        // ALSO: display the border around the tile
        boxScript.DisplayBorder();

        /******************************************************************
         * FEATURE: Obstructions- disable / enable button depending on
         *          word's validity
         ******************************************************************/
        if (GameManagerScript.OBSTRUCTION_PRODUCTIVE || GameManagerScript.OBSTRUCTION_UNPRODUCTIVE)
        {
            GameManagerScript.gameManager.UpdatePlayButton();
        }

        /*****************************************************************
         * FEATURE: Shiny particles whenever you select a tile!
         * Productive Juice. Unproductive juice produces particles every touch
         *****************************************************************/
        if (GameManagerScript.JUICE_PRODUCTIVE)
        {
            boxScript.PlayShinySelectParticles();
        }

        /******************************************************************
         * FEATURE: juice: turn on this flag so that the tile
         *          bounces around/animates when selected
         ******************************************************************/
        boxScript.isSelected = true;

        //=====================================================================
        //  FEATURE: Juice: extra animations/bouncing if tile is selected.
        //=====================================================================
        if (GameManagerScript.JUICE_UNPRODUCTIVE || GameManagerScript.JUICE_PRODUCTIVE)
        {
            // Animate the tile
            boxScript.StartCoroutine(boxScript.AnimateBouncingTile());
        }

        //=====================================================================
        //  FEATURE: Unproductive Juice: camera shakes and explosions when
        //           selecting letters.
        //=====================================================================
        if (GameManagerScript.JUICE_UNPRODUCTIVE)
        {
            CameraShaker.Instance.ShakeOnce(3f, 3.5f, .1f, .3f);
        }

        // Play sound effect
        AudioManager.instance.Play("Select");

        DisplaySelectedScore();
    }

    public static void DisplayHighlightFeedback()
    {
        Color highlightColor = GetHighlightColor(currentWord);
        foreach (Vector2 v in currentSelection)
        {
            grid[(int)v.x, (int)v.y].gameObject.transform.Find("Block_bg").GetComponent<SpriteRenderer>().color = highlightColor;
        }
    }

    public static void DisplaySelectedScore()
    {
        // Calculate currently selected score and change the text on screen
        if (currentWord.Length >= 3)
        {
            long currentScore = GetScore(currentWord, null);
            if (currentScore == 0)
            {
                selectedScore.text = "";
            }
            else
            {
                selectedScore.text = currentWord + ": " + currentScore + " pts";
            }
        }
        else
        {
            selectedScore.text = "";
        }
    }

    public static bool IsNextTo(Vector2 someLoc, Vector2 otherLoc)
    {
        int myX = (int)someLoc.x;
        int myY = (int)someLoc.y;
        int otherX = (int)otherLoc.x;
        int otherY = (int)otherLoc.y;

        // TODO: refactor and use an array of dx[] dy[] to shorten the code

        // check to the right
        if (myX + 1 == otherX && myY == otherY)
        {
            return true;
        }
        // check to the left
        else if (myX - 1 == otherX && myY == otherY)
        {
            return true;
        }
        // check up
        else if (myX == otherX && myY + 1 == otherY)
        {
            return true;
        }
        // check down
        else if (myX == otherX && myY - 1 == otherY)
        {
            return true;
        }
        // check diagonal top right
        else if (myX + 1 == otherX && myY + 1 == otherY)
        {
            return true;
        }
        // check diagonal top left
        else if (myX - 1 == otherX && myY + 1 == otherY)
        {
            return true;
        }
        // check diagonal bottom right
        else if (myX + 1 == otherX && myY - 1 == otherY)
        {
            return true;
        }
        // check diagonal bottom left
        else if (myX - 1 == otherX && myY - 1 == otherY)
        {
            return true;
        }

        return false;
    }

    // Checks to see if the other tile is adjacent (or diagonal) to the current location
    public bool IsNextTo(Vector2 otherLoc)
    {
        int otherX = (int)otherLoc.x;
        int otherY = (int)otherLoc.y;

        // TODO: refactor and use an array of dx[] dy[] to shorten the code

        // check to the right
        if (myX + 1 == otherX && myY == otherY)
        {
            return true;
        }
        // check to the left
        else if (myX - 1 == otherX && myY == otherY)
        {
            return true;
        }
        // check up
        else if (myX == otherX && myY + 1 == otherY)
        {
            return true;
        }
        // check down
        else if (myX == otherX && myY - 1 == otherY)
        {
            return true;
        }
        // check diagonal top right
        else if (myX + 1 == otherX && myY + 1 == otherY)
        {
            return true;
        }
        // check diagonal top left
        else if (myX - 1 == otherX && myY + 1 == otherY)
        {
            return true;
        }
        // check diagonal bottom right
        else if (myX + 1 == otherX && myY - 1 == otherY)
        {
            return true;
        }
        // check diagonal bottom left
        else if (myX - 1 == otherX && myY - 1 == otherY)
        {
            return true;
        }

        return false;
    }

    public static void ClearAllSelectedTiles()
    {
        currentWord = "";

        // remove all coloring
        foreach (Vector2 v in currentSelection)
        {
            BoxScript box = grid[(int)v.x, (int)v.y].gameObject.GetComponent<BoxScript>();
            box.ResetTileColors();
            box.isSelected = false;
        }

        currentSelection.Clear();

        /******************************************************************
         * FEATURE: Display currently selected score
         ******************************************************************/
        // Calculate currently selected score and change the text on screen
        selectedScore.text = "";

        /******************************************************************
         * FEATURE: Disable the play word button if it exists
         ******************************************************************/
        if (GameManagerScript.OBSTRUCTION_PRODUCTIVE || GameManagerScript.OBSTRUCTION_UNPRODUCTIVE)
        {
            GameManagerScript.gameManager.UpdatePlayButton();
        }
    }

    public static string GetLetterFromPrefab(string name)
    {
        // kind of a hack.. the prefab names' 7th character is the letter of the block
        return name.Substring(6, 1);
    }

    public static bool IsInsideGrid(Vector2 pos)
    {
        float x = pos.x;
        int y = (int)pos.y;
        return (x >= -gridWidthRadius && x <= gridWidthRadius && y >= -gridHeightRadius && y <= gridHeightRadius);
    }

    public static bool IsColumnFull(int x)
    {
        for (int y = 0; y < gridHeight; ++y)
        {
            if (grid[x, y] == null)
            {
                return false;
            }
        }

        return true;
    }

    bool IsValidPosition()
    {
        Vector2 v = transform.position;

        if (!IsInsideGrid(v))
        {
            return false;
        }
        if (grid[(int)(v.x + gridWidthRadius), (int)v.y + gridHeightRadius] != null &&
            grid[(int)(v.x + gridWidthRadius), (int)v.y + gridHeightRadius] != transform)
        {
            return false;
        }

        return true;
    }

    void ColumnDown()
    {
        columnFalling = true;

        // move every other block on top of this block down 1 as well
        for (int y = myY; y < gridHeight; ++y)
        {
            if (grid[myX, y] != null)
            {
                grid[myX, y].position += new Vector3(0, -1, 0);
                grid[myX, y].gameObject.GetComponent<BoxScript>().myY -= 1;
                grid[myX, y - 1] = grid[myX, y];
            }
            else
            {
                grid[myX, y - 1] = null;
            }
        }

        grid[myX, gridHeight - 1] = null;
    }

    void GridUpdate()
    {
        // Remove the previous location of this block from the grid
        grid[myX, myY] = null;

        // Add the new location of the block to the grid
        Vector2 v = transform.position;
        myX = (int)(v.x + gridWidthRadius);
        myY = (int)v.y + gridHeightRadius;
        grid[myX, myY] = transform;
    }

    public static void Reset()
    {
        foreach (Transform transform in grid)
        {
            if (transform != null && transform.gameObject != null)
            {
                Destroy(transform.gameObject);
            }
        }

        grid = new Transform[gridWidth, gridHeight];
        currentWord = "";
        currentSelection.Clear();
        score = 0;
        scoreText.text = "Points: " + score;
        selectedScore.text = "";
    }


    // TODO: move these two functions to a different class
    public static string GetBoardPayload()
    {
        string boardString = "";

        for (int j = gridHeight - 1; j >= 0; --j)
        {
            for (int i = 0; i < gridWidth; ++i)
            {
                if (grid[i, j] != null)
                {
                    boardString += grid[i, j].gameObject.GetComponent<BoxScript>().Letter;
                }
            }

            if (j > 0)
            {
                boardString += "\n";
            }
        }

        return boardString;
    }

    public static char[,] GetBoardLetters()
    {
        char[,] letters = new char[gridWidth, gridHeight];

        for (int j = 0; j < gridHeight; ++j)
        {
            for (int i = 0; i < gridWidth; ++i)
            {
                if (grid[i, j] != null)
                {
                    letters[i, j] = grid[i, j].gameObject.GetComponent<BoxScript>().Letter[0];
                }
            }
        }

        return letters;
    }
}
