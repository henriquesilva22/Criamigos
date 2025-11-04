using System;
using System.Collections;
using UnityEngine;
using Criamigos.Core;
using Criamigos.AI;
using Criamigos.UI;

namespace Criamigos.Core
{
    /// <summary>
    /// Gerenciador central do jogo que coordena todos os sistemas
    /// Controla fluxo do jogo, inicialização, estados e integração entre componentes
    /// Mantém arquitetura limpa com separação de responsabilidades
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Configurações do Jogo")]
        [SerializeField] private bool autoStartGame = true; // Inicia jogo automaticamente
        [SerializeField] private bool skipMainMenu = false; // Pula menu principal (debug)
        [SerializeField] private float initializationTimeout = 30f; // Timeout para inicialização
        
        [Header("Referências dos Sistemas")]
        [SerializeField] private ConfigLoader configLoader;
        [SerializeField] private CreatureController creatureController;
        [SerializeField] private AIResponder aiResponder;
        [SerializeField] private TextToSpeechManager ttsManager;
        [SerializeField] private CreatureEvolution evolutionSystem;
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private UIManager uiManager;
        
        [Header("Configurações de Criatura")]
        [SerializeField] private string defaultCreatureName = "Amiguinho";
        [SerializeField] private CreatureEvolution.CreatureColor defaultColor = CreatureEvolution.CreatureColor.Blue;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugMode = false;
        [SerializeField] private bool showDebugUI = false;
        
        // Estado do jogo
        public enum GameState
        {
            Initializing,    // Carregando sistemas
            MainMenu,        // No menu principal
            Playing,         // Jogando normalmente
            Paused,          // Jogo pausado
            Error,           // Estado de erro
            Saving,          // Salvando dados
            Loading          // Carregando dados
        }
        
        // Estado atual
        private GameState currentState = GameState.Initializing;
        private bool isSystemsReady = false;
        private bool isGameReady = false;
        
        // Eventos
        public event Action<GameState> OnGameStateChanged;
        public event Action OnSystemsInitialized;
        public event Action OnGameReady;
        public event Action<string> OnGameError;
        
        // Singleton
        public static GameManager Instance { get; private set; }
        
        void Awake()
        {
            // Implementa singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Inicializa sistemas
            StartCoroutine(InitializeSystems());
        }
        
        void Start()
        {
            if (enableDebugMode)
                Debug.Log("[GameManager] Iniciando GameManager...");
        }
        
        void Update()
        {
            // Verifica input de debug
            if (enableDebugMode && Input.GetKeyDown(KeyCode.F1))
            {
                showDebugUI = !showDebugUI;
            }
            
            // Pausa com ESC (só em desktop)
            if (Input.GetKeyDown(KeyCode.Escape) && currentState == GameState.Playing)
            {
                PauseGame();
            }
        }
        
        void OnGUI()
        {
            if (!enableDebugMode || !showDebugUI) return;
            
            // UI de debug
            GUI.Box(new Rect(10, 10, 300, 200), "GameManager Debug");
            GUI.Label(new Rect(20, 35, 280, 20), $"Estado: {currentState}");
            GUI.Label(new Rect(20, 55, 280, 20), $"Sistemas OK: {isSystemsReady}");
            GUI.Label(new Rect(20, 75, 280, 20), $"Jogo Pronto: {isGameReady}");
            
            if (GUI.Button(new Rect(20, 100, 100, 30), "Salvar"))
            {
                SaveGame();
            }
            
            if (GUI.Button(new Rect(130, 100, 100, 30), "Carregar"))
            {
                LoadGame();
            }
            
            if (GUI.Button(new Rect(20, 140, 100, 30), "Reset"))
            {
                ResetGame();
            }
        }
        
        /// <summary>
        /// Inicializa todos os sistemas do jogo
        /// </summary>
        private IEnumerator InitializeSystems()
        {
            ChangeGameState(GameState.Initializing);
            
            float startTime = Time.time;
            bool timedOut = false;
            
            if (enableDebugMode)
                Debug.Log("[GameManager] Iniciando inicialização dos sistemas...");
            
            // 1. Encontra componentes automaticamente se não foram assignados
            yield return StartCoroutine(FindSystemComponents());
            
            // 2. Inicializa ConfigLoader primeiro
            yield return StartCoroutine(InitializeConfigLoader());
            
            // 3. Aguarda sistemas principais
            yield return StartCoroutine(InitializeCoreComponents());
            
            // 4. Inicializa sistemas de IA
            yield return StartCoroutine(InitializeAISystems());
            
            // 5. Configura conexões entre sistemas
            yield return StartCoroutine(SetupSystemConnections());
            
            // 6. Carrega dados salvos
            yield return StartCoroutine(LoadGameData());
            
            // 7. Verifica timeout
            if (Time.time - startTime > initializationTimeout)
            {
                timedOut = true;
                OnGameError?.Invoke("Timeout na inicialização dos sistemas");
            }
            
            if (!timedOut)
            {
                isSystemsReady = true;
                OnSystemsInitialized?.Invoke();
                
                if (enableDebugMode)
                    Debug.Log("[GameManager] Todos os sistemas inicializados com sucesso!");
                
                // Decide próximo estado
                if (skipMainMenu || autoStartGame)
                {
                    StartGame();
                }
                else
                {
                    ChangeGameState(GameState.MainMenu);
                }
            }
            else
            {
                ChangeGameState(GameState.Error);
            }
        }
        
        /// <summary>
        /// Encontra componentes automaticamente
        /// </summary>
        private IEnumerator FindSystemComponents()
        {
            if (enableDebugMode)
                Debug.Log("[GameManager] Procurando componentes dos sistemas...");
            
            if (configLoader == null)
                configLoader = FindObjectOfType<ConfigLoader>();
                
            if (creatureController == null)
                creatureController = FindObjectOfType<CreatureController>();
                
            if (aiResponder == null)
                aiResponder = FindObjectOfType<AIResponder>();
                
            if (ttsManager == null)
                ttsManager = FindObjectOfType<TextToSpeechManager>();
                
            if (evolutionSystem == null)
                evolutionSystem = FindObjectOfType<CreatureEvolution>();
                
            if (saveSystem == null)
                saveSystem = FindObjectOfType<SaveSystem>();
                
            if (uiManager == null)
                uiManager = FindObjectOfType<UIManager>();
            
            yield return null;
        }
        
        /// <summary>
        /// Inicializa ConfigLoader
        /// </summary>
        private IEnumerator InitializeConfigLoader()
        {
            if (configLoader == null)
            {
                Debug.LogWarning("[GameManager] ConfigLoader não encontrado, criando dinamicamente...");
                
                GameObject configObj = new GameObject("ConfigLoader");
                configLoader = configObj.AddComponent<ConfigLoader>();
            }
            
            // Aguarda configuração carregar
            float waitTime = 0f;
            while (!configLoader.IsConfigLoaded() && waitTime < 10f)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }
            
            if (!configLoader.IsConfigLoaded())
            {
                Debug.LogWarning("[GameManager] ConfigLoader não carregou a tempo, usando configurações padrão");
            }
        }
        
        /// <summary>
        /// Inicializa componentes principais
        /// </summary>
        private IEnumerator InitializeCoreComponents()
        {
            if (enableDebugMode)
                Debug.Log("[GameManager] Inicializando componentes principais...");
            
            // SaveSystem
            if (saveSystem != null)
            {
                saveSystem.Initialize();
            }
            
            // CreatureEvolution
            if (evolutionSystem != null)
            {
                evolutionSystem.Initialize();
            }
            
            // CreatureController
            if (creatureController != null)
            {
                creatureController.Initialize();
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        /// <summary>
        /// Inicializa sistemas de IA
        /// </summary>
        private IEnumerator InitializeAISystems()
        {
            if (enableDebugMode)
                Debug.Log("[GameManager] Inicializando sistemas de IA...");
            
            // AIResponder
            if (aiResponder != null && configLoader != null)
            {
                if (configLoader.HasAPIKeys())
                {
                    aiResponder.Initialize();
                }
                else
                {
                    Debug.LogWarning("[GameManager] Chaves de API não configuradas, IA funcionará em modo offline");
                }
            }
            
            // TextToSpeechManager
            if (ttsManager != null)
            {
                ttsManager.Initialize();
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        /// <summary>
        /// Configura conexões entre sistemas
        /// </summary>
        private IEnumerator SetupSystemConnections()
        {
            if (enableDebugMode)
                Debug.Log("[GameManager] Configurando conexões entre sistemas...");
            
            // Conecta UI com sistemas
            if (uiManager != null)
            {
                SetupUIConnections();
            }
            
            // Conecta eventos entre sistemas
            SetupSystemEvents();
            
            yield return null;
        }
        
        /// <summary>
        /// Configura conexões da UI
        /// </summary>
        private void SetupUIConnections()
        {
            if (uiManager == null) return;
            
            // Conecta eventos da UI com ações da criatura
            uiManager.OnFeedClicked += () => {
                if (creatureController != null)
                    creatureController.Feed();
            };
            
            uiManager.OnPlayClicked += () => {
                if (creatureController != null)
                    creatureController.Play();
            };
            
            uiManager.OnSleepClicked += () => {
                if (creatureController != null)
                    creatureController.Sleep();
            };
            
            uiManager.OnTalkClicked += () => {
                if (creatureController != null && aiResponder != null)
                    creatureController.StartConversation();
            };
            
            uiManager.OnListenClicked += () => {
                if (creatureController != null)
                    creatureController.ListenToUser();
            };
        }
        
        /// <summary>
        /// Configura eventos entre sistemas
        /// </summary>
        private void SetupSystemEvents()
        {
            // Eventos de evolução
            if (evolutionSystem != null && uiManager != null)
            {
                evolutionSystem.OnColorChanged += (color) => {
                    uiManager.SetCreatureColor(color);
                };
            }
            
            // Eventos de save automático
            if (saveSystem != null && evolutionSystem != null)
            {
                evolutionSystem.OnLevelUp += (level) => {
                    SaveGame();
                };
            }
            
            // Eventos de IA
            if (aiResponder != null && uiManager != null)
            {
                aiResponder.OnResponseReceived += (response) => {
                    uiManager.ShowMessage(response);
                };
            }
        }
        
        /// <summary>
        /// Carrega dados do jogo
        /// </summary>
        private IEnumerator LoadGameData()
        {
            if (enableDebugMode)
                Debug.Log("[GameManager] Carregando dados do jogo...");
            
            if (saveSystem != null)
            {
                var gameData = saveSystem.LoadGameData();
                
                if (gameData != null)
                {
                    // Aplica dados carregados aos sistemas
                    ApplyLoadedData(gameData);
                    
                    if (enableDebugMode)
                        Debug.Log("[GameManager] Dados carregados com sucesso");
                }
                else
                {
                    // Cria novo jogo se não há save
                    CreateNewGame();
                    
                    if (enableDebugMode)
                        Debug.Log("[GameManager] Criando novo jogo");
                }
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Aplica dados carregados aos sistemas
        /// </summary>
        private void ApplyLoadedData(SaveSystem.GameData gameData)
        {
            if (gameData == null) return;
            
            // Aplica dados da criatura
            if (creatureController != null && gameData.creatureData != null)
            {
                creatureController.LoadFromSave(gameData.creatureData);
            }
            
            // Aplica dados de evolução
            if (evolutionSystem != null && gameData.evolutionData != null)
            {
                evolutionSystem.LoadFromSave(gameData.evolutionData);
            }
        }
        
        /// <summary>
        /// Cria novo jogo
        /// </summary>
        private void CreateNewGame()
        {
            if (creatureController != null)
            {
                creatureController.InitializeNewCreature(defaultCreatureName, defaultColor);
            }
            
            if (evolutionSystem != null)
            {
                evolutionSystem.ResetEvolution();
            }
        }
        
        /// <summary>
        /// Inicia o jogo
        /// </summary>
        public void StartGame()
        {
            if (!isSystemsReady)
            {
                Debug.LogError("[GameManager] Tentando iniciar jogo antes dos sistemas estarem prontos");
                return;
            }
            
            ChangeGameState(GameState.Playing);
            isGameReady = true;
            
            // Mostra tela do jogo
            if (uiManager != null)
            {
                uiManager.ShowGameScreen();
            }
            
            // Inicia sistemas ativos
            if (creatureController != null)
            {
                creatureController.StartGame();
            }
            
            OnGameReady?.Invoke();
            
            if (enableDebugMode)
                Debug.Log("[GameManager] Jogo iniciado!");
        }
        
        /// <summary>
        /// Pausa o jogo
        /// </summary>
        public void PauseGame()
        {
            if (currentState != GameState.Playing) return;
            
            ChangeGameState(GameState.Paused);
            Time.timeScale = 0f;
            
            if (enableDebugMode)
                Debug.Log("[GameManager] Jogo pausado");
        }
        
        /// <summary>
        /// Resume o jogo
        /// </summary>
        public void ResumeGame()
        {
            if (currentState != GameState.Paused) return;
            
            ChangeGameState(GameState.Playing);
            Time.timeScale = 1f;
            
            if (enableDebugMode)
                Debug.Log("[GameManager] Jogo resumido");
        }
        
        /// <summary>
        /// Salva o jogo
        /// </summary>
        public void SaveGame()
        {
            if (saveSystem == null || !isGameReady) return;
            
            GameState previousState = currentState;
            ChangeGameState(GameState.Saving);
            
            try
            {
                saveSystem.SaveGame();
                
                if (enableDebugMode)
                    Debug.Log("[GameManager] Jogo salvo com sucesso");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] Erro ao salvar jogo: {e.Message}");
            }
            finally
            {
                ChangeGameState(previousState);
            }
        }
        
        /// <summary>
        /// Carrega o jogo
        /// </summary>
        public void LoadGame()
        {
            if (saveSystem == null) return;
            
            GameState previousState = currentState;
            ChangeGameState(GameState.Loading);
            
            try
            {
                var gameData = saveSystem.LoadGameData();
                
                if (gameData != null)
                {
                    ApplyLoadedData(gameData);
                    
                    if (enableDebugMode)
                        Debug.Log("[GameManager] Jogo carregado com sucesso");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] Erro ao carregar jogo: {e.Message}");
            }
            finally
            {
                ChangeGameState(previousState);
            }
        }
        
        /// <summary>
        /// Reseta o jogo
        /// </summary>
        public void ResetGame()
        {
            if (enableDebugMode)
                Debug.Log("[GameManager] Resetando jogo...");
            
            // Para sistemas
            if (creatureController != null)
                creatureController.ResetCreature();
                
            if (evolutionSystem != null)
                evolutionSystem.ResetEvolution();
            
            // Limpa saves
            if (saveSystem != null)
                saveSystem.ResetAllProgress();
            
            // Recria jogo
            CreateNewGame();
            
            if (enableDebugMode)
                Debug.Log("[GameManager] Jogo resetado");
        }
        
        /// <summary>
        /// Muda estado do jogo
        /// </summary>
        private void ChangeGameState(GameState newState)
        {
            if (currentState == newState) return;
            
            GameState oldState = currentState;
            currentState = newState;
            
            if (enableDebugMode)
                Debug.Log($"[GameManager] Estado mudou: {oldState} -> {newState}");
            
            OnGameStateChanged?.Invoke(newState);
        }
        
        /// <summary>
        /// Métodos públicos para acesso ao estado
        /// </summary>
        public GameState GetCurrentState()
        {
            return currentState;
        }
        
        public bool IsSystemsReady()
        {
            return isSystemsReady;
        }
        
        public bool IsGameReady()
        {
            return isGameReady;
        }
        
        /// <summary>
        /// Obtém referência de um sistema específico
        /// </summary>
        public T GetSystem<T>() where T : MonoBehaviour
        {
            if (typeof(T) == typeof(ConfigLoader)) return configLoader as T;
            if (typeof(T) == typeof(CreatureController)) return creatureController as T;
            if (typeof(T) == typeof(AIResponder)) return aiResponder as T;
            if (typeof(T) == typeof(TextToSpeechManager)) return ttsManager as T;
            if (typeof(T) == typeof(CreatureEvolution)) return evolutionSystem as T;
            if (typeof(T) == typeof(SaveSystem)) return saveSystem as T;
            if (typeof(T) == typeof(UIManager)) return uiManager as T;
            
            return null;
        }
        
        /// <summary>
        /// Cleanup quando aplicação fecha
        /// </summary>
        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && currentState == GameState.Playing)
            {
                SaveGame(); // Auto-save quando minimiza
            }
        }
        
        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && currentState == GameState.Playing)
            {
                SaveGame(); // Auto-save quando perde foco
            }
        }
        
        void OnDestroy()
        {
            // Salva antes de destruir
            if (isGameReady && currentState == GameState.Playing)
            {
                SaveGame();
            }
        }
    }
}