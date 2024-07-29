using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace MatchThreeEngine
{
    public sealed class Board : MonoBehaviour
    {
        [SerializeField] private TileTypeAsset[] tileTypes;
        [SerializeField] private Transform swappingOverlay;
        [SerializeField] private Row[] rows;
        [SerializeField] private AudioClip matchBeep;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float tweenDuration;
        [SerializeField] private bool ensureNoStartingMatches;
        [SerializeField] private float gameDuration = 120f;

        private readonly List<Tile> _selectedTiles = new List<Tile>();
        private bool _isTileSwapping;
        public GameObject eventGame;
        private bool _isTileMatching;
        private bool _isBoardShuffling;
        private bool _isGameActive; 
        public int score;
        public TMP_Text scoreText;
        public TMP_Text winTimeText;
        public TMP_Text loseScoreText;
        public TMP_Text timerText;
        public GameObject winPanel;
        public GameObject losePanel;
        private float _remainingTime;
        private Coroutine _countdownCoroutine;
        public int currentLevel = 1;
        public Button[] levelButtons;
        public int levelAmount = 1;
        public GameObject levelMenu;

        public event Action<TileTypeAsset, int> OnMatch;

        private TileData[,] Matrix
        {
            get
            {
                var width = rows.Max(row => row.tiles.Length);
                var height = rows.Length;

                var data = new TileData[width, height];

                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                        data[x, y] = GetTile(x, y).Data;

                return data;
            }
        }

        private void Start()
        {
            LoadLevels();
            UpdateButtonInteractivity();
        }

        public void SelectLevel(int levelSelected)
        {
            currentLevel = levelSelected;
            levelMenu.SetActive(false);
            BeginGame();
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        private void SaveLevels()
        {
            PlayerPrefs.SetInt("LevelsCompleted", levelAmount);
            PlayerPrefs.Save();
        }

        private void LoadLevels()
        {
            levelAmount = PlayerPrefs.GetInt("LevelsCompleted", 1);
        }

        private void UpdateButtonInteractivity()
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                levelButtons[i].interactable = i < levelAmount;
            }
        }

        public void NextLevel()
        {
            if (currentLevel < 30)
            {
                currentLevel++;
            }
            BeginGame();
        }

        public void BeginGame()
        {
            for (var y = 0; y < rows.Length; y++)
            {
                for (var x = 0; x < rows.Max(row => row.tiles.Length); x++)
                {
                    var tile = GetTile(x, y);
                    tile.x = x;
                    tile.y = y;
                    tile.Type = tileTypes[Random.Range(0, tileTypes.Length)];
                    tile.button.onClick.AddListener(() => SelectTile(tile));
                }
            }

            score = 100;
            scoreText.text = "Points left: " + score;

            if (ensureNoStartingMatches) StartCoroutine(EnsureNoInitialMatches());

            StopCountdown();
            _remainingTime = gameDuration;
            _isGameActive = true;
            _countdownCoroutine = StartCoroutine(CountdownCoroutine());
        }

        private IEnumerator CountdownCoroutine()
        {
            while (_remainingTime > 0 && _isGameActive)
            {
                _remainingTime -= Time.deltaTime;
                int seconds = (int)_remainingTime;
                timerText.text = "Time: " + seconds + "s"; // Update timer text

                if (_remainingTime <= 0)
                {
                    EndGame();
                }

                yield return null;
            }
        }

        public void StopCountdown()
        {
            _isGameActive = false;
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
            }
        }

        public void ResumeCountdown()
        {
            _isGameActive = true;
            _countdownCoroutine = StartCoroutine(CountdownCoroutine());
        }

        private void WinGame()
        {
            winTimeText.text = "Time: " + (int)_remainingTime + "s";
            winPanel.SetActive(true);
            StopCountdown();
        }

        public void AfterWin()
        {
            if (levelAmount < 30)
            {
                levelAmount++;
            }
            SaveLevels();
            UpdateButtonInteractivity();
        }

        private void EndGame()
        {
            loseScoreText.text = "Points left: " + score;
            losePanel.SetActive(true);
            StopCountdown();
        }

        private Tile GetTile(int x, int y) => rows[y].tiles[x];

        private Tile[] GetTiles(IList<TileData> tileData)
        {
            var length = tileData.Count;
            var tiles = new Tile[length];

            for (var i = 0; i < length; i++) tiles[i] = GetTile(tileData[i].X, tileData[i].Y);

            return tiles;
        }

        private IEnumerator EnsureNoInitialMatches()
        {
            var wait = new WaitForEndOfFrame();

            while (TileDataMatrixUtility.FindBestMatch(Matrix) != null)
            {
                ShuffleBoard();
                yield return wait;
            }
        }

        private async void SelectTile(Tile tile)
        {
            if (_isTileSwapping || _isTileMatching || _isBoardShuffling)
            {
                Debug.Log("Action in progress, selection ignored.");
                return;
            }

            if (!_selectedTiles.Contains(tile))
            {
                if (_selectedTiles.Count > 0)
                {
                    if (Math.Abs(tile.x - _selectedTiles[0].x) == 1 && Math.Abs(tile.y - _selectedTiles[0].y) == 0
                        || Math.Abs(tile.y - _selectedTiles[0].y) == 1 && Math.Abs(tile.x - _selectedTiles[0].x) == 0)
                    {
                        _selectedTiles.Add(tile);
                    }
                }
                else
                {
                    _selectedTiles.Add(tile);
                }
            }

            if (_selectedTiles.Count < 2) return;

            _isTileSwapping = true;
            bool success = await SwapAndMatchTilesAsync(_selectedTiles[0], _selectedTiles[1]);
            if (!success)
            {
                await SwapTilesAsync(_selectedTiles[0], _selectedTiles[1]);
            }
            _isTileSwapping = false;

            _selectedTiles.Clear();
            EnsurePlayableBoard();
        }

        private async Task<bool> SwapAndMatchTilesAsync(Tile tile1, Tile tile2)
        {
            await SwapTilesAsync(tile1, tile2);

            if (await TryMatchTilesAsync())
            {
                return true;
            }

            return false;
        }

        private async Task SwapTilesAsync(Tile tile1, Tile tile2)
        {
            var icon1 = tile1.icon;
            var icon2 = tile2.icon;

            var icon1Transform = icon1.transform;
            var icon2Transform = icon2.transform;

            icon1Transform.SetParent(swappingOverlay);
            icon2Transform.SetParent(swappingOverlay);

            icon1Transform.SetAsLastSibling();
            icon2Transform.SetAsLastSibling();

            icon1Transform.SetParent(tile2.transform);
            icon2Transform.SetParent(tile1.transform);

            tile1.icon = icon2;
            tile2.icon = icon1;

            var tile1Item = tile1.Type;

            tile1.Type = tile2.Type;
            tile2.Type = tile1Item;
        }

        private void EnsurePlayableBoard()
        {
            var matrix = Matrix;

            while (TileDataMatrixUtility.FindBestMatch(matrix) != null)
            {
                ShuffleBoard();
                matrix = Matrix;
            }
        }

        private void ShuffleBoard()
        {
            _isBoardShuffling = true;

            foreach (var row in rows)
                foreach (var tile in row.tiles)
                    tile.Type = tileTypes[Random.Range(0, tileTypes.Length)];

            _isBoardShuffling = false;
        }

        public void UpdateScore()
        {
            score -= 4;
            scoreText.text = "Points left: " + score;
            if (score <= 0)
            {
                WinGame();
            }
        }

        private async Task<bool> TryMatchTilesAsync()
        {
            var didMatch = false;
            _isTileMatching = true;
            var match = TileDataMatrixUtility.FindBestMatch(Matrix);

            while (match != null)
            {
                didMatch = true;
                var tiles = GetTiles(match.Tiles);
                var deflateSequence = DOTween.Sequence();

                foreach (var tile in tiles) deflateSequence.Join(tile.icon.transform.DOScale(Vector3.zero, tweenDuration).SetEase(Ease.InBack));

                audioSource.PlayOneShot(matchBeep);
                await deflateSequence.Play().AsyncWaitForCompletion();

                var inflateSequence = DOTween.Sequence();
                foreach (var tile in tiles)
                {
                    tile.Type = tileTypes[Random.Range(0, tileTypes.Length)];
                    inflateSequence.Join(tile.icon.transform.DOScale(Vector3.one, tweenDuration).SetEase(Ease.OutBack));
                }
                UpdateScore();

                await inflateSequence.Play().AsyncWaitForCompletion();

                OnMatch?.Invoke(Array.Find(tileTypes, tileType => tileType.id == match.TypeId), match.Tiles.Length);

                match = TileDataMatrixUtility.FindBestMatch(Matrix);
            }
            _isTileMatching = false;

            return didMatch;
        }

    }
}
