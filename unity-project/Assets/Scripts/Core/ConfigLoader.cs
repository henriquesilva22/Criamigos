using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Criamigos.Core
{
    /// <summary>
    /// Sistema de carregamento de configurações que gerencia chaves de API e ajustes do jogo
    /// Carrega configurações de Resources e permite override por arquivos externos
    /// Mantém segurança das chaves de API e configurações sensíveis
    /// </summary>
    public class ConfigLoader : MonoBehaviour
    {
        [Header("Arquivos de Configuração")]
        [SerializeField] private string configResourcePath = "config"; // Caminho em Resources/
        [SerializeField] private string externalConfigName = "config.json"; // Nome do arquivo externo
        [SerializeField] private bool allowExternalOverride = true; // Permite override por arquivo externo
        
        [Header("Configurações de Segurança")]
        [SerializeField] private bool validateAPIKeys = true; // Valida formato das chaves
        [SerializeField] private bool hideKeysInLogs = true; // Esconde chaves nos logs
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true; // Logs detalhados
        
        // Eventos
        public event Action OnConfigLoaded; // Disparado quando config é carregado
        public event Action<string> OnConfigError; // Disparado quando há erro
        
        // Estado interno
        private GameConfig loadedConfig;
        private bool isConfigLoaded = false;
        
        /// <summary>
        /// Estrutura principal de configuração do jogo
        /// </summary>
        [System.Serializable]
        public class GameConfig
        {
            [Header("APIs de IA")]
            public string openai_api_key = ""; // Chave da OpenAI
            public string openai_organization = ""; // Organização OpenAI (opcional)
            public string custom_ai_endpoint = ""; // Endpoint customizado para IA
            
            [Header("Configurações de TTS")]
            public TTSConfig tts = new TTSConfig();
            
            [Header("Configurações de STT")]
            public STTConfig stt = new STTConfig();
            
            [Header("Configurações de IA")]
            public AIConfig ai = new AIConfig();
            
            [Header("Configurações do Jogo")]
            public GameSettings game = new GameSettings();
        }
        
        [System.Serializable]
        public class TTSConfig
        {
            public string voice = "nova"; // Voz padrão
            public float speed = 0.9f; // Velocidade da fala
            public string format = "mp3"; // Formato de áudio
            public string model = "tts-1"; // Modelo de TTS
            public float volume = 0.8f; // Volume padrão
        }
        
        [System.Serializable]
        public class STTConfig
        {
            public string model = "whisper-1"; // Modelo de STT
            public string language = "pt"; // Idioma padrão
            public float timeout = 30f; // Timeout das requisições
            public bool enable_noise_reduction = true; // Redução de ruído
        }
        
        [System.Serializable]
        public class AIConfig
        {
            public string model = "gpt-3.5-turbo"; // Modelo de IA
            public float temperature = 0.7f; // Temperatura (criatividade)
            public int max_tokens = 50; // Máximo de tokens por resposta
            public float timeout = 15f; // Timeout das requisições
            public bool enable_content_filter = true; // Filtro de conteúdo
        }
        
        [System.Serializable]
        public class GameSettings
        {
            public string default_language = "pt-BR"; // Idioma padrão
            public bool enable_analytics = false; // Analytics (sempre false para crianças)
            public bool enable_crash_reporting = true; // Relatórios de crash
            public float auto_save_interval = 300f; // Intervalo de auto-save
            public bool enable_offline_mode = true; // Permite modo offline
        }
        
        void Awake()
        {
            // Garante singleton
            var existingLoader = FindObjectOfType<ConfigLoader>();
            if (existingLoader != null && existingLoader != this)
            {
                Destroy(gameObject);
                return;
            }
            
            DontDestroyOnLoad(gameObject);
        }
        
        void Start()
        {
            StartCoroutine(LoadConfiguration());
        }
        
        /// <summary>
        /// Carrega configuração de múltiplas fontes
        /// </summary>
        private IEnumerator LoadConfiguration()
        {
            if (enableDebugLogs)
                Debug.Log("[ConfigLoader] Iniciando carregamento de configurações...");
            
            // 1. Tenta carregar configuração padrão dos Resources
            yield return StartCoroutine(LoadFromResources());
            
            // 2. Se permitido, tenta override com arquivo externo
            if (allowExternalOverride)
            {
                yield return StartCoroutine(LoadFromExternalFile());
            }
            
            // 3. Valida configuração carregada
            if (ValidateConfiguration())
            {
                isConfigLoaded = true;
                OnConfigLoaded?.Invoke();
                
                if (enableDebugLogs)
                    Debug.Log("[ConfigLoader] Configuração carregada com sucesso");
            }
            else
            {
                OnConfigError?.Invoke("Configuração inválida ou incompleta");
            }
        }
        
        /// <summary>
        /// Carrega configuração padrão dos Resources
        /// </summary>
        private IEnumerator LoadFromResources()
        {
            try
            {
                TextAsset configAsset = Resources.Load<TextAsset>(configResourcePath);
                
                if (configAsset != null)
                {
                    string configJson = configAsset.text;
                    loadedConfig = JsonUtility.FromJson<GameConfig>(configJson);
                    
                    if (enableDebugLogs)
                        Debug.Log("[ConfigLoader] Configuração base carregada dos Resources");
                }
                else
                {
                    // Cria configuração padrão se não encontrar arquivo
                    loadedConfig = CreateDefaultConfig();
                    
                    if (enableDebugLogs)
                        Debug.LogWarning("[ConfigLoader] Arquivo de configuração não encontrado, usando padrões");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigLoader] Erro ao carregar dos Resources: {e.Message}");
                loadedConfig = CreateDefaultConfig();
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Tenta carregar arquivo externo para override de configurações
        /// </summary>
        private IEnumerator LoadFromExternalFile()
        {
            string externalPath = Path.Combine(Application.streamingAssetsPath, externalConfigName);
            
            if (enableDebugLogs)
                Debug.Log($"[ConfigLoader] Procurando arquivo externo: {externalPath}");
            
            using (UnityWebRequest request = UnityWebRequest.Get(externalPath))
            {
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string externalJson = request.downloadHandler.text;
                        GameConfig externalConfig = JsonUtility.FromJson<GameConfig>(externalJson);
                        
                        // Faz merge da configuração externa com a base
                        MergeConfigurations(externalConfig);
                        
                        if (enableDebugLogs)
                            Debug.Log("[ConfigLoader] Configuração externa aplicada com sucesso");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ConfigLoader] Erro ao processar arquivo externo: {e.Message}");
                    }
                }
                else
                {
                    if (enableDebugLogs)
                        Debug.Log("[ConfigLoader] Arquivo externo não encontrado, usando configuração base");
                }
            }
        }
        
        /// <summary>
        /// Cria configuração padrão quando não há arquivos
        /// </summary>
        private GameConfig CreateDefaultConfig()
        {
            return new GameConfig
            {
                openai_api_key = "", // Vazio por padrão - deve ser configurado pelo usuário
                tts = new TTSConfig
                {
                    voice = "nova",
                    speed = 0.9f,
                    format = "mp3",
                    model = "tts-1",
                    volume = 0.8f
                },
                stt = new STTConfig
                {
                    model = "whisper-1",
                    language = "pt",
                    timeout = 30f,
                    enable_noise_reduction = true
                },
                ai = new AIConfig
                {
                    model = "gpt-3.5-turbo",
                    temperature = 0.7f,
                    max_tokens = 50,
                    timeout = 15f,
                    enable_content_filter = true
                },
                game = new GameSettings
                {
                    default_language = "pt-BR",
                    enable_analytics = false, // Sempre false para crianças
                    enable_crash_reporting = true,
                    auto_save_interval = 300f,
                    enable_offline_mode = true
                }
            };
        }
        
        /// <summary>
        /// Faz merge da configuração externa com a base
        /// </summary>
        private void MergeConfigurations(GameConfig externalConfig)
        {
            if (externalConfig == null) return;
            
            // Merge de chaves de API
            if (!string.IsNullOrEmpty(externalConfig.openai_api_key))
                loadedConfig.openai_api_key = externalConfig.openai_api_key;
                
            if (!string.IsNullOrEmpty(externalConfig.openai_organization))
                loadedConfig.openai_organization = externalConfig.openai_organization;
                
            if (!string.IsNullOrEmpty(externalConfig.custom_ai_endpoint))
                loadedConfig.custom_ai_endpoint = externalConfig.custom_ai_endpoint;
            
            // Merge configurações de TTS
            if (externalConfig.tts != null)
            {
                if (!string.IsNullOrEmpty(externalConfig.tts.voice))
                    loadedConfig.tts.voice = externalConfig.tts.voice;
                    
                if (externalConfig.tts.speed > 0)
                    loadedConfig.tts.speed = externalConfig.tts.speed;
                    
                if (!string.IsNullOrEmpty(externalConfig.tts.format))
                    loadedConfig.tts.format = externalConfig.tts.format;
                    
                if (externalConfig.tts.volume > 0)
                    loadedConfig.tts.volume = externalConfig.tts.volume;
            }
            
            // Merge configurações de AI
            if (externalConfig.ai != null)
            {
                if (!string.IsNullOrEmpty(externalConfig.ai.model))
                    loadedConfig.ai.model = externalConfig.ai.model;
                    
                if (externalConfig.ai.temperature >= 0)
                    loadedConfig.ai.temperature = externalConfig.ai.temperature;
                    
                if (externalConfig.ai.max_tokens > 0)
                    loadedConfig.ai.max_tokens = externalConfig.ai.max_tokens;
                    
                if (externalConfig.ai.timeout > 0)
                    loadedConfig.ai.timeout = externalConfig.ai.timeout;
            }
            
            // Configurações de jogo sempre preservam segurança para crianças
            if (externalConfig.game != null)
            {
                if (!string.IsNullOrEmpty(externalConfig.game.default_language))
                    loadedConfig.game.default_language = externalConfig.game.default_language;
                    
                if (externalConfig.game.auto_save_interval > 0)
                    loadedConfig.game.auto_save_interval = externalConfig.game.auto_save_interval;
                    
                // NUNCA permite analytics para crianças, mesmo se especificado
                loadedConfig.game.enable_analytics = false;
            }
        }
        
        /// <summary>
        /// Valida se a configuração carregada é válida
        /// </summary>
        private bool ValidateConfiguration()
        {
            if (loadedConfig == null) return false;
            
            // Valida chaves de API se habilitado
            if (validateAPIKeys && !string.IsNullOrEmpty(loadedConfig.openai_api_key))
            {
                if (!IsValidAPIKey(loadedConfig.openai_api_key))
                {
                    Debug.LogWarning("[ConfigLoader] Chave de API OpenAI parece inválida");
                }
            }
            
            // Valida configurações críticas
            if (loadedConfig.ai.max_tokens <= 0 || loadedConfig.ai.max_tokens > 1000)
            {
                Debug.LogWarning("[ConfigLoader] max_tokens fora do range seguro, ajustando para 50");
                loadedConfig.ai.max_tokens = 50;
            }
            
            if (loadedConfig.ai.temperature < 0 || loadedConfig.ai.temperature > 2)
            {
                Debug.LogWarning("[ConfigLoader] temperature fora do range, ajustando para 0.7");
                loadedConfig.ai.temperature = 0.7f;
            }
            
            return true;
        }
        
        /// <summary>
        /// Valida formato básico de chave de API
        /// </summary>
        private bool IsValidAPIKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            
            // Validação básica para chave OpenAI
            if (key.StartsWith("sk-") && key.Length > 20)
            {
                return true;
            }
            
            return false;
        }
        
        // Métodos públicos para acesso às configurações
        
        /// <summary>
        /// Obtém chave de API para um serviço específico
        /// </summary>
        public string GetAPIKey(string service)
        {
            if (loadedConfig == null) return "";
            
            switch (service.ToLower())
            {
                case "openai":
                    return loadedConfig.openai_api_key;
                default:
                    return "";
            }
        }
        
        /// <summary>
        /// Obtém configurações de TTS
        /// </summary>
        public TTSConfig GetTTSConfig()
        {
            return loadedConfig?.tts;
        }
        
        /// <summary>
        /// Obtém configurações de STT
        /// </summary>
        public STTConfig GetSTTConfig()
        {
            return loadedConfig?.stt;
        }
        
        /// <summary>
        /// Obtém configurações de IA
        /// </summary>
        public AIConfig GetAIConfig()
        {
            return loadedConfig?.ai;
        }
        
        /// <summary>
        /// Obtém configurações do jogo
        /// </summary>
        public GameSettings GetGameSettings()
        {
            return loadedConfig?.game;
        }
        
        /// <summary>
        /// Verifica se as configurações foram carregadas
        /// </summary>
        public bool IsConfigLoaded()
        {
            return isConfigLoaded;
        }
        
        /// <summary>
        /// Obtém configuração completa (para debug)
        /// </summary>
        public GameConfig GetFullConfig()
        {
            return loadedConfig;
        }
        
        /// <summary>
        /// Verifica se tem chaves de API configuradas
        /// </summary>
        public bool HasAPIKeys()
        {
            return loadedConfig != null && !string.IsNullOrEmpty(loadedConfig.openai_api_key);
        }
        
        /// <summary>
        /// Obtém configuração como JSON (com chaves censuradas para logs)
        /// </summary>
        public string GetConfigAsJson(bool hideKeys = true)
        {
            if (loadedConfig == null) return "{}";
            
            var configCopy = JsonUtility.FromJson<GameConfig>(JsonUtility.ToJson(loadedConfig));
            
            if (hideKeys && hideKeysInLogs)
            {
                // Censura chaves para logs
                if (!string.IsNullOrEmpty(configCopy.openai_api_key))
                    configCopy.openai_api_key = "sk-***HIDDEN***";
            }
            
            return JsonUtility.ToJson(configCopy, true);
        }
        
        /// <summary>
        /// Força recarregamento das configurações
        /// </summary>
        public void ReloadConfiguration()
        {
            isConfigLoaded = false;
            StartCoroutine(LoadConfiguration());
        }
    }
}