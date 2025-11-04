using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Criamigos.AI
{
    /// <summary>
    /// Sistema de Text-to-Speech otimizado para crianças com cache inteligente e fallbacks offline
    /// Converte texto em áudio falado usando APIs externas ou sons pré-gravados
    /// Gerencia filas de reprodução e sincronização com animações da criatura
    /// </summary>
    public class TextToSpeechManager : MonoBehaviour
    {
        [Header("Componentes de Áudio")]
        [SerializeField] private AudioSource primaryAudioSource; // AudioSource principal para TTS
        [SerializeField] private AudioSource fallbackAudioSource; // AudioSource para sons de fallback
        
        [Header("Configurações da API TTS")]
        [SerializeField] private string ttsEndpoint = "https://api.openai.com/v1/audio/speech"; // URL da API TTS
        [SerializeField] private string voiceId = "nova"; // Voz infantil da OpenAI (nova, alloy)
        [SerializeField] private float speechSpeed = 0.9f; // Velocidade mais lenta para crianças (0.25-4.0)
        [SerializeField] private string audioFormat = "mp3"; // Formato de áudio (mp3, opus, aac, flac)
        [SerializeField] private float requestTimeoutSeconds = 20f; // Timeout para requisições
        
        [Header("Cache e Performance")]
        [SerializeField] private bool enableAudioCache = true; // Ativa cache de áudios gerados
        [SerializeField] private int maxCacheSize = 25; // Máximo de áudios no cache
        [SerializeField] private bool preloadCommonPhrases = true; // Pré-carrega frases comuns
        [SerializeField] private float maxCacheAgeDays = 7f; // Idade máxima do cache em dias
        
        [Header("Fallbacks Offline")]
        [SerializeField] private AudioClip[] happySounds; // Sons alegres para quando não há TTS
        [SerializeField] private AudioClip[] sadSounds; // Sons tristes 
        [SerializeField] private AudioClip[] hungrySounds; // Sons de fome
        [SerializeField] private AudioClip[] tiredSounds; // Sons de cansaço
        [SerializeField] private AudioClip[] genericSounds; // Sons genéricos
        
        [Header("Configurações de Reprodução")]
        [SerializeField] private float volumeMultiplier = 0.8f; // Volume das falas (0-1)
        [SerializeField] private bool enableSpatialAudio = false; // Áudio 3D espacial
        [SerializeField] private AnimationCurve volumeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0); // Curva de volume
        
        [Header("Debug e Testes")]
        [SerializeField] private bool enableDebugLogs = true; // Logs detalhados
        [SerializeField] private bool simulateOfflineMode = false; // Força modo offline
        [SerializeField] private bool showCacheStats = false; // Mostra estatísticas do cache
        
        // Eventos públicos para sincronização
        public event Action<string> OnSpeechStarted; // Disparado quando inicia fala (passa o texto)
        public event Action OnSpeechCompleted; // Disparado quando termina fala
        public event Action<AudioClip> OnAudioGenerated; // Disparado quando gera novo áudio
        public event Action<string> OnError; // Disparado quando ocorre erro
        public event Action OnCacheCleared; // Disparado quando cache é limpo
        
        // Sistema de cache e estado interno
        private Dictionary<string, CachedAudio> audioCache; // Cache de áudios com metadados
        private Queue<SpeechRequest> speechQueue; // Fila de requisições de fala
        private bool isSpeaking = false; // Flag de estado de reprodução
        private bool isGeneratingAudio = false; // Flag de geração de áudio
        private string apiKey = ""; // Chave da API carregada dinamicamente
        private string cacheDirectory; // Diretório do cache persistente
        private System.Random randomGenerator; // Gerador para fallbacks aleatórios
        
        // Estatísticas do sistema
        private int totalRequests = 0;
        private int cacheHits = 0;
        private int onlineRequests = 0;
        private int fallbackUses = 0;
        
        /// <summary>
        /// Estrutura para armazenar áudios em cache com metadados
        /// </summary>
        [System.Serializable]
        private class CachedAudio
        {
            public AudioClip audioClip; // O clip de áudio
            public string filePath; // Caminho do arquivo no disco
            public long creationTime; // Timestamp de criação
            public int useCount; // Quantas vezes foi usado
            public string originalText; // Texto original que gerou o áudio
            
            public CachedAudio(AudioClip clip, string path, string text)
            {
                audioClip = clip;
                filePath = path;
                creationTime = System.DateTimeOffset.Now.ToUnixTimeSeconds();
                useCount = 0;
                originalText = text;
            }
        }
        
        /// <summary>
        /// Estrutura para requisições de fala na fila
        /// </summary>
        private class SpeechRequest
        {
            public string text; // Texto para falar
            public string emotion; // Emoção associada (para fallbacks)
            public float priority; // Prioridade (0-1, maior = mais prioritário)
            
            public SpeechRequest(string text, string emotion = "", float priority = 0.5f)
            {
                this.text = text;
                this.emotion = emotion;
                this.priority = priority;
            }
        }
        
        void Awake()
        {
            // Inicializa componentes
            audioCache = new Dictionary<string, CachedAudio>();
            speechQueue = new Queue<SpeechRequest>();
            randomGenerator = new System.Random();
            
            // Define diretório do cache
            cacheDirectory = Path.Combine(Application.persistentDataPath, "TTSCache");
            
            // Cria diretório se não existir
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }
        }
        
        void Start()
        {
            // Configura AudioSources se não foram atribuídos
            SetupAudioSources();
            
            // Carrega configurações da API
            StartCoroutine(LoadTTSConfiguration());
            
            // Carrega cache persistente
            StartCoroutine(LoadPersistentCache());
            
            // Pré-carrega frases comuns se habilitado
            if (preloadCommonPhrases)
            {
                StartCoroutine(PreloadCommonPhrases());
            }
            
            if (enableDebugLogs)
                Debug.Log("[TextToSpeechManager] Sistema iniciado - Cache em: " + cacheDirectory);
        }
        
        /// <summary>
        /// Configura AudioSources automaticamente se não foram atribuídos
        /// </summary>
        private void SetupAudioSources()
        {
            if (primaryAudioSource == null)
            {
                primaryAudioSource = gameObject.AddComponent<AudioSource>();
                primaryAudioSource.playOnAwake = false;
                primaryAudioSource.volume = volumeMultiplier;
                primaryAudioSource.spatialBlend = enableSpatialAudio ? 1f : 0f;
            }
            
            if (fallbackAudioSource == null)
            {
                fallbackAudioSource = gameObject.AddComponent<AudioSource>();
                fallbackAudioSource.playOnAwake = false;
                fallbackAudioSource.volume = volumeMultiplier * 0.8f; // Fallbacks um pouco mais baixos
                fallbackAudioSource.spatialBlend = enableSpatialAudio ? 1f : 0f;
            }
        }
        
        /// <summary>
        /// Carrega configurações de TTS do ConfigLoader
        /// </summary>
        private IEnumerator LoadTTSConfiguration()
        {
            var configLoader = FindObjectOfType<Core.ConfigLoader>();
            if (configLoader != null)
            {
                yield return new WaitUntil(() => configLoader.IsConfigLoaded());
                
                apiKey = configLoader.GetAPIKey("openai");
                
                // Carrega configurações específicas de TTS se disponíveis
                var ttsConfig = configLoader.GetTTSConfig();
                if (ttsConfig != null)
                {
                    voiceId = ttsConfig.voice ?? voiceId;
                    speechSpeed = ttsConfig.speed > 0 ? ttsConfig.speed : speechSpeed;
                    audioFormat = ttsConfig.format ?? audioFormat;
                }
                
                if (enableDebugLogs)
                {
                    if (string.IsNullOrEmpty(apiKey))
                        Debug.LogWarning("[TextToSpeechManager] Chave da API não encontrada - usando apenas fallbacks");
                    else
                        Debug.Log("[TextToSpeechManager] Configuração TTS carregada com sucesso");
                }
            }
            else
            {
                Debug.LogWarning("[TextToSpeechManager] ConfigLoader não encontrado");
            }
        }
        
        /// <summary>
        /// Carrega cache persistente do disco
        /// </summary>
        private IEnumerator LoadPersistentCache()
        {
            if (!enableAudioCache) yield break;
            
            string[] cacheFiles = Directory.GetFiles(cacheDirectory, "*.wav");
            int loadedCount = 0;
            
            foreach (string filePath in cacheFiles)
            {
                // Verifica idade do arquivo
                var fileInfo = new FileInfo(filePath);
                var ageInDays = (DateTime.Now - fileInfo.CreationTime).TotalDays;
                
                if (ageInDays > maxCacheAgeDays)
                {
                    // Remove arquivos muito antigos
                    File.Delete(filePath);
                    continue;
                }
                
                // Tenta carregar o áudio
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string originalText = DecodeTextFromFileName(fileName);
                
                if (!string.IsNullOrEmpty(originalText))
                {
                    yield return StartCoroutine(LoadAudioFromFile(filePath, originalText));
                    loadedCount++;
                }
                
                // Limita carregamento por frame para não travar
                if (loadedCount % 3 == 0)
                    yield return null;
            }
            
            if (enableDebugLogs && loadedCount > 0)
                Debug.Log($"[TextToSpeechManager] Cache carregado: {loadedCount} áudios");
        }
        
        /// <summary>
        /// Pré-carrega frases comuns para resposta mais rápida
        /// </summary>
        private IEnumerator PreloadCommonPhrases()
        {
            string[] commonPhrases = {
                "Oi! Como você está?",
                "Que legal! Adorei!",
                "Estou com fome!",
                "Vamos brincar!",
                "Preciso descansar...",
                "Você é meu melhor amigo!",
                "Obrigado por cuidar de mim!",
                "Que divertido!"
            };
            
            foreach (string phrase in commonPhrases)
            {
                if (!audioCache.ContainsKey(phrase.ToLower()))
                {
                    // Agenda pré-carregamento com baixa prioridade
                    EnqueueSpeech(phrase, "", 0.1f);
                    yield return new WaitForSeconds(2f); // Espaça as requisições
                }
            }
            
            if (enableDebugLogs)
                Debug.Log("[TextToSpeechManager] Pré-carregamento de frases comuns concluído");
        }
        
        /// <summary>
        /// Método principal para converter texto em fala
        /// </summary>
        /// <param name="text">Texto para converter em fala</param>
        /// <param name="emotion">Emoção para fallbacks (happy, sad, hungry, tired)</param>
        /// <param name="priority">Prioridade da requisição (0-1)</param>
        public void Speak(string text, string emotion = "", float priority = 0.5f)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[TextToSpeechManager] Texto vazio enviado para TTS");
                return;
            }
            
            totalRequests++;
            
            // Limpa texto e normaliza
            text = CleanAndNormalizeText(text);
            
            // Verifica cache primeiro
            if (enableAudioCache && audioCache.ContainsKey(text.ToLower()))
            {
                cacheHits++;
                PlayCachedAudio(text);
                return;
            }
            
            // Se já está falando, adiciona à fila
            if (isSpeaking || isGeneratingAudio)
            {
                EnqueueSpeech(text, emotion, priority);
                return;
            }
            
            // Processa imediatamente
            StartCoroutine(ProcessSpeechRequest(new SpeechRequest(text, emotion, priority)));
        }
        
        /// <summary>
        /// Adiciona requisição à fila de fala
        /// </summary>
        private void EnqueueSpeech(string text, string emotion, float priority)
        {
            speechQueue.Enqueue(new SpeechRequest(text, emotion, priority));
            
            if (enableDebugLogs)
                Debug.Log($"[TextToSpeechManager] Adicionado à fila: {text} (Fila: {speechQueue.Count})");
        }
        
        /// <summary>
        /// Processa uma requisição de fala
        /// </summary>
        private IEnumerator ProcessSpeechRequest(SpeechRequest request)
        {
            OnSpeechStarted?.Invoke(request.text);
            
            // Tenta gerar áudio via API se disponível
            if (ShouldUseOnlineTTS())
            {
                yield return StartCoroutine(GenerateOnlineAudio(request));
            }
            else
            {
                // Usa fallback offline
                PlayFallbackAudio(request.emotion);
                fallbackUses++;
            }
            
            // Processa próximo item da fila
            yield return StartCoroutine(ProcessNextInQueue());
        }
        
        /// <summary>
        /// Verifica se deve tentar usar TTS online
        /// </summary>
        private bool ShouldUseOnlineTTS()
        {
            return !simulateOfflineMode && 
                   !string.IsNullOrEmpty(apiKey) && 
                   Application.internetReachability != NetworkReachability.NotReachable;
        }
        
        /// <summary>
        /// Gera áudio usando API online
        /// </summary>
        private IEnumerator GenerateOnlineAudio(SpeechRequest request)
        {
            isGeneratingAudio = true;
            
            if (enableDebugLogs)
                Debug.Log($"[TextToSpeechManager] Gerando áudio online: {request.text}");
            
            // Cria payload para API
            var requestData = new
            {
                model = "tts-1",
                input = request.text,
                voice = voiceId,
                speed = speechSpeed,
                response_format = audioFormat
            };
            
            string jsonPayload = JsonUtility.ToJson(requestData);
            
            using (UnityWebRequest webRequest = new UnityWebRequest(ttsEndpoint, "POST"))
            {
                // Configura requisição
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                webRequest.timeout = (int)requestTimeoutSeconds;
                
                // Envia requisição
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    // Converte bytes em AudioClip
                    byte[] audioData = webRequest.downloadHandler.data;
                    yield return StartCoroutine(CreateAudioClipFromBytes(audioData, request.text));
                    onlineRequests++;
                }
                else
                {
                    Debug.LogWarning($"[TextToSpeechManager] Erro na API TTS: {webRequest.error}");
                    PlayFallbackAudio(request.emotion);
                    fallbackUses++;
                }
            }
            
            isGeneratingAudio = false;
        }
        
        /// <summary>
        /// Cria AudioClip a partir de bytes de áudio
        /// </summary>
        private IEnumerator CreateAudioClipFromBytes(byte[] audioData, string originalText)
        {
            // Salva temporariamente no cache
            string tempFileName = GenerateFileName(originalText);
            string tempPath = Path.Combine(cacheDirectory, tempFileName + "." + audioFormat);
            
            try
            {
                File.WriteAllBytes(tempPath, audioData);
                
                // Carrega como AudioClip
                using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, GetAudioType()))
                {
                    yield return audioRequest.SendWebRequest();
                    
                    if (audioRequest.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip audioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
                        
                        if (audioClip != null)
                        {
                            // Adiciona ao cache
                            if (enableAudioCache)
                            {
                                CacheAudioClip(originalText, audioClip, tempPath);
                            }
                            
                            // Reproduz o áudio
                            PlayAudioClip(audioClip);
                            OnAudioGenerated?.Invoke(audioClip);
                        }
                        else
                        {
                            Debug.LogError("[TextToSpeechManager] Falha ao criar AudioClip");
                            PlayFallbackAudio("");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[TextToSpeechManager] Erro ao carregar áudio: {audioRequest.error}");
                        PlayFallbackAudio("");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TextToSpeechManager] Erro ao processar áudio: {e.Message}");
                PlayFallbackAudio("");
            }
        }
        
        /// <summary>
        /// Reproduz áudio do cache
        /// </summary>
        private void PlayCachedAudio(string text)
        {
            var cachedAudio = audioCache[text.ToLower()];
            cachedAudio.useCount++;
            
            if (enableDebugLogs)
                Debug.Log($"[TextToSpeechManager] Usando cache: {text}");
            
            PlayAudioClip(cachedAudio.audioClip);
        }
        
        /// <summary>
        /// Reproduz um AudioClip através do AudioSource
        /// </summary>
        private void PlayAudioClip(AudioClip clip)
        {
            if (primaryAudioSource != null && clip != null)
            {
                isSpeaking = true;
                primaryAudioSource.clip = clip;
                primaryAudioSource.volume = volumeMultiplier;
                primaryAudioSource.Play();
                
                // Monitora conclusão da reprodução
                StartCoroutine(MonitorAudioPlayback(clip.length));
            }
        }
        
        /// <summary>
        /// Reproduz áudio de fallback baseado na emoção
        /// </summary>
        private void PlayFallbackAudio(string emotion)
        {
            AudioClip[] soundArray = GetFallbackSounds(emotion);
            
            if (soundArray != null && soundArray.Length > 0)
            {
                AudioClip randomSound = soundArray[randomGenerator.Next(soundArray.Length)];
                
                if (fallbackAudioSource != null && randomSound != null)
                {
                    isSpeaking = true;
                    fallbackAudioSource.clip = randomSound;
                    fallbackAudioSource.Play();
                    
                    StartCoroutine(MonitorAudioPlayback(randomSound.length));
                    
                    if (enableDebugLogs)
                        Debug.Log($"[TextToSpeechManager] Usando fallback: {emotion}");
                }
            }
            else
            {
                // Se não há sons de fallback, apenas marca como concluído
                OnSpeechCompleted?.Invoke();
            }
        }
        
        /// <summary>
        /// Obtém sons de fallback baseados na emoção
        /// </summary>
        private AudioClip[] GetFallbackSounds(string emotion)
        {
            switch (emotion.ToLower())
            {
                case "happy": case "alegre": return happySounds;
                case "sad": case "triste": return sadSounds;
                case "hungry": case "fome": return hungrySounds;
                case "tired": case "cansado": return tiredSounds;
                default: return genericSounds;
            }
        }
        
        /// <summary>
        /// Monitora a reprodução de áudio e dispara eventos
        /// </summary>
        private IEnumerator MonitorAudioPlayback(float duration)
        {
            yield return new WaitForSeconds(duration);
            
            isSpeaking = false;
            OnSpeechCompleted?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log("[TextToSpeechManager] Reprodução concluída");
        }
        
        /// <summary>
        /// Processa próximo item da fila
        /// </summary>
        private IEnumerator ProcessNextInQueue()
        {
            yield return new WaitForSeconds(0.5f); // Pequena pausa entre falas
            
            if (speechQueue.Count > 0 && !isSpeaking && !isGeneratingAudio)
            {
                SpeechRequest nextRequest = speechQueue.Dequeue();
                yield return StartCoroutine(ProcessSpeechRequest(nextRequest));
            }
        }
        
        // Métodos utilitários
        
        /// <summary>
        /// Limpa e normaliza texto para TTS
        /// </summary>
        private string CleanAndNormalizeText(string text)
        {
            // Remove caracteres especiais problemáticos
            text = text.Replace("\"", "").Replace("\n", " ").Replace("\r", "");
            
            // Normaliza espaços
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            
            return text.Trim();
        }
        
        /// <summary>
        /// Gera nome de arquivo baseado no texto
        /// </summary>
        private string GenerateFileName(string text)
        {
            // Cria hash do texto para nome único
            var hash = text.GetHashCode().ToString();
            return $"tts_{hash}";
        }
        
        /// <summary>
        /// Obtém tipo de áudio baseado no formato configurado
        /// </summary>
        private AudioType GetAudioType()
        {
            switch (audioFormat.ToLower())
            {
                case "mp3": return AudioType.MPEG;
                case "wav": return AudioType.WAV;
                case "ogg": return AudioType.OGGVORBIS;
                default: return AudioType.MPEG;
            }
        }
        
        // Métodos públicos para controle externo
        
        /// <summary>
        /// Para a fala atual e limpa a fila
        /// </summary>
        public void StopSpeaking()
        {
            if (primaryAudioSource != null) primaryAudioSource.Stop();
            if (fallbackAudioSource != null) fallbackAudioSource.Stop();
            
            isSpeaking = false;
            speechQueue.Clear();
            
            OnSpeechCompleted?.Invoke();
        }
        
        /// <summary>
        /// Verifica se está falando
        /// </summary>
        public bool IsSpeaking()
        {
            return isSpeaking || isGeneratingAudio;
        }
        
        /// <summary>
        /// Limpa cache de áudios
        /// </summary>
        public void ClearCache()
        {
            audioCache.Clear();
            
            // Remove arquivos do disco
            try
            {
                string[] files = Directory.GetFiles(cacheDirectory);
                foreach (string file in files)
                {
                    File.Delete(file);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TextToSpeechManager] Erro ao limpar cache: {e.Message}");
            }
            
            OnCacheCleared?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log("[TextToSpeechManager] Cache limpo");
        }
        
        /// <summary>
        /// Obtém estatísticas do sistema
        /// </summary>
        public string GetCacheStats()
        {
            return $"Cache: {audioCache.Count}/{maxCacheSize} | " +
                   $"Requisições: {totalRequests} | " +
                   $"Cache hits: {cacheHits} | " +
                   $"Online: {onlineRequests} | " +
                   $"Fallbacks: {fallbackUses}";
        }
        
        void Update()
        {
            // Mostra estatísticas do cache se habilitado
            if (showCacheStats && enableDebugLogs)
            {
                // Atualiza a cada 5 segundos
                if (Time.time % 5f < Time.deltaTime)
                {
                    Debug.Log($"[TextToSpeechManager] {GetCacheStats()}");
                }
            }
        }
        
        // Métodos auxiliares para cache persistente
        
        private void CacheAudioClip(string text, AudioClip clip, string filePath)
        {
            if (audioCache.Count >= maxCacheSize)
            {
                RemoveOldestCacheEntry();
            }
            
            audioCache[text.ToLower()] = new CachedAudio(clip, filePath, text);
        }
        
        private void RemoveOldestCacheEntry()
        {
            // Remove entrada mais antiga ou menos usada
            string keyToRemove = "";
            long oldestTime = long.MaxValue;
            
            foreach (var kvp in audioCache)
            {
                if (kvp.Value.creationTime < oldestTime)
                {
                    oldestTime = kvp.Value.creationTime;
                    keyToRemove = kvp.Key;
                }
            }
            
            if (!string.IsNullOrEmpty(keyToRemove))
            {
                var cachedAudio = audioCache[keyToRemove];
                
                // Remove arquivo do disco
                if (File.Exists(cachedAudio.filePath))
                {
                    File.Delete(cachedAudio.filePath);
                }
                
                audioCache.Remove(keyToRemove);
            }
        }
        
        private IEnumerator LoadAudioFromFile(string filePath, string originalText)
        {
            using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.WAV))
            {
                yield return audioRequest.SendWebRequest();
                
                if (audioRequest.result == UnityWebRequest.Result.Success)
                {
                    AudioClip audioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
                    if (audioClip != null)
                    {
                        audioCache[originalText.ToLower()] = new CachedAudio(audioClip, filePath, originalText);
                    }
                }
            }
        }
        
        private string DecodeTextFromFileName(string fileName)
        {
            // Implementação simples - em versão completa poderia usar metadata
            if (fileName.StartsWith("tts_"))
            {
                return "cached_audio"; // Placeholder - idealmente salvaria metadata
            }
            return "";
        }
    }
}