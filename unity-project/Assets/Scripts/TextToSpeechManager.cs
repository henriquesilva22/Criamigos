using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gerenciador de Text-to-Speech que converte texto em áudio falado
/// Suporta APIs de TTS online e reprodução através do AudioSource
/// Inclui cache de áudios e controles de reprodução
/// </summary>
public class TextToSpeechManager : MonoBehaviour
{
    [Header("Componentes")]
    [SerializeField] private AudioSource audioSource; // AudioSource que vai tocar o áudio do TTS
    
    [Header("Configurações da API")]
    [SerializeField] private string apiEndpoint = "https://api.openai.com/v1/audio/speech"; // URL da API de TTS
    [SerializeField] private string configFileName = "config.json"; // Arquivo de configuração em StreamingAssets
    [SerializeField] private float requestTimeoutSeconds = 30f; // Tempo limite para requisições
    
    [Header("Configurações de Voz")]
    [SerializeField] private string voiceModel = "tts-1"; // Modelo de TTS a usar
    [SerializeField] private string voiceName = "nova"; // Nome da voz (alloy, echo, fable, onyx, nova, shimmer)
    [SerializeField] private float speechSpeed = 1.0f; // Velocidade da fala (0.25 - 4.0)
    [SerializeField] private AudioClip.LoadType loadType = AudioClip.LoadType.DecompressOnLoad; // Como carregar o áudio
    
    [Header("Configurações de Cache")]
    [SerializeField] private bool enableAudioCache = true; // Ativa cache de áudios já gerados
    [SerializeField] private int maxCacheSize = 10; // Máximo de áudios no cache
    
    [Header("Configurações de Fallback")]
    [SerializeField] private AudioClip[] fallbackSounds; // Sons alternativos quando TTS falha
    [SerializeField] private bool useTextDisplay = true; // Mostra texto na tela quando TTS falha
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true; // Ativa logs detalhados
    
    // Eventos para outros sistemas escutarem
    public event Action<string> OnSpeechStarted; // Disparado quando inicia a fala (passa o texto)
    public event Action OnSpeechCompleted; // Disparado quando termina a fala
    public event Action<string> OnError; // Disparado quando ocorre erro
    public event Action<AudioClip> OnAudioGenerated; // Disparado quando gera novo áudio
    
    // Estado interno
    private string apiKey = ""; // Chave da API carregada do arquivo de configuração
    private bool isSpeaking = false; // Flag que indica se está falando
    private bool isGenerating = false; // Flag que indica se está gerando áudio
    private System.Collections.Generic.Dictionary<string, AudioClip> audioCache; // Cache de áudios gerados
    private System.Collections.Generic.Queue<string> speechQueue; // Fila de textos para falar
    
    // Configuração carregada do arquivo JSON
    [System.Serializable]
    private class ConfigData
    {
        public string openai_api_key; // Chave da API OpenAI
        public string tts_endpoint; // Endpoint personalizado (opcional)
        public string tts_voice; // Voz preferida
        public float tts_speed; // Velocidade da fala
    }
    
    void Awake()
    {
        // Inicializa o cache e a fila
        audioCache = new System.Collections.Generic.Dictionary<string, AudioClip>();
        speechQueue = new System.Collections.Generic.Queue<string>();
        
        // Se não foi atribuído no Inspector, pega o AudioSource deste GameObject
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }
    
    void Start()
    {
        // Carrega configurações do arquivo StreamingAssets
        LoadConfiguration();
        
        // Verifica se tem AudioSource
        if (audioSource == null)
        {
            Debug.LogError("AudioSource não encontrado! Adicione um AudioSource ao GameObject.");
            OnError?.Invoke("AudioSource não configurado");
        }
    }
    
    /// <summary>
    /// Carrega as configurações de API do arquivo config.json
    /// </summary>
    private async void LoadConfiguration()
    {
        try
        {
            // Constrói o caminho para o arquivo de configuração
            string configPath = System.IO.Path.Combine(Application.streamingAssetsPath, configFileName);
            
            if (enableDebugLogs)
                Debug.Log($"Carregando configuração TTS de: {configPath}");
            
            // Usa UnityWebRequest para carregar o arquivo
            using (UnityWebRequest www = UnityWebRequest.Get(configPath))
            {
                var operation = www.SendWebRequest();
                
                // Aguarda o carregamento de forma assíncrona
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Erro ao carregar configuração TTS: {www.error}");
                    OnError?.Invoke("Arquivo de configuração TTS não encontrado");
                    return;
                }
                
                // Faz parse do JSON
                ConfigData config = JsonUtility.FromJson<ConfigData>(www.downloadHandler.text);
                apiKey = config.openai_api_key;
                
                // Usa configurações personalizadas se especificadas
                if (!string.IsNullOrEmpty(config.tts_endpoint))
                    apiEndpoint = config.tts_endpoint;
                
                if (!string.IsNullOrEmpty(config.tts_voice))
                    voiceName = config.tts_voice;
                
                if (config.tts_speed > 0)
                    speechSpeed = config.tts_speed;
                
                if (enableDebugLogs)
                    Debug.Log("Configurações TTS carregadas com sucesso!");
                
                // Verifica se a chave foi carregada
                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.LogWarning("Chave da API TTS não configurada. Usando apenas fallbacks.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao carregar configuração TTS: {e.Message}");
            OnError?.Invoke("Erro na configuração TTS");
        }
    }
    
    /// <summary>
    /// Converte texto em fala e reproduz o áudio
    /// </summary>
    public async Task<bool> Speak(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("Texto vazio enviado para TTS!");
            return false;
        }
        
        // Se já está falando, adiciona à fila
        if (isSpeaking || isGenerating)
        {
            speechQueue.Enqueue(text);
            if (enableDebugLogs)
                Debug.Log($"Texto adicionado à fila: {text}");
            return true;
        }
        
        try
        {
            if (enableDebugLogs)
                Debug.Log($"Iniciando TTS para: {text}");
            
            OnSpeechStarted?.Invoke(text);
            
            // Verifica se já tem no cache
            if (enableAudioCache && audioCache.ContainsKey(text))
            {
                if (enableDebugLogs)
                    Debug.Log("Usando áudio do cache");
                
                PlayAudioClip(audioCache[text]);
                return true;
            }
            
            // Gera novo áudio via API
            AudioClip audioClip = await GenerateAudioFromAPI(text);
            
            if (audioClip != null)
            {
                // Adiciona ao cache se habilitado
                if (enableAudioCache)
                {
                    AddToCache(text, audioClip);
                }
                
                // Reproduz o áudio
                PlayAudioClip(audioClip);
                OnAudioGenerated?.Invoke(audioClip);
                return true;
            }
            else
            {
                // Usa fallback se a API falhou
                HandleTTSFallback(text);
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro no TTS: {e.Message}");
            OnError?.Invoke($"Erro TTS: {e.Message}");
            HandleTTSFallback(text);
            return false;
        }
    }
    
    /// <summary>
    /// Gera áudio através da API de TTS
    /// </summary>
    private async Task<AudioClip> GenerateAudioFromAPI(string text)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            if (enableDebugLogs)
                Debug.Log("Chave da API não configurada, usando fallback");
            return null;
        }
        
        isGenerating = true;
        
        try
        {
            // Constrói o payload JSON para a API
            var requestData = new
            {
                model = voiceModel,
                input = text,
                voice = voiceName,
                speed = speechSpeed,
                response_format = "mp3"
            };
            
            string jsonData = JsonUtility.ToJson(requestData);
            
            using (UnityWebRequest request = new UnityWebRequest(apiEndpoint, "POST"))
            {
                // Configura o request
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                request.timeout = (int)requestTimeoutSeconds;
                
                // Envia a requisição
                var operation = request.SendWebRequest();
                
                // Aguarda a conclusão de forma assíncrona
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                // Verifica se houve erro
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Erro na API TTS: {request.error}");
                    return null;
                }
                
                // Converte os bytes de áudio em AudioClip
                byte[] audioData = request.downloadHandler.data;
                return await CreateAudioClipFromBytes(audioData);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao gerar áudio: {e.Message}");
            return null;
        }
        finally
        {
            isGenerating = false;
        }
    }
    
    /// <summary>
    /// Cria AudioClip a partir de bytes de áudio MP3
    /// </summary>
    private async Task<AudioClip> CreateAudioClipFromBytes(byte[] audioBytes)
    {
        try
        {
            // Salva temporariamente o arquivo para carregar como AudioClip
            string tempPath = System.IO.Path.Combine(Application.persistentDataPath, "temp_tts.mp3");
            System.IO.File.WriteAllBytes(tempPath, audioBytes);
            
            // Carrega o AudioClip usando UnityWebRequest
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
            {
                var operation = www.SendWebRequest();
                
                // Aguarda o carregamento
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Erro ao carregar AudioClip: {www.error}");
                    return null;
                }
                
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                
                // Remove o arquivo temporário
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
                
                return clip;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao criar AudioClip: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Reproduz um AudioClip através do AudioSource
    /// </summary>
    private void PlayAudioClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            isSpeaking = true;
            audioSource.clip = clip;
            audioSource.Play();
            
            // Inicia coroutine para detectar quando termina
            StartCoroutine(WaitForSpeechCompletion(clip.length));
        }
    }
    
    /// <summary>
    /// Aguarda a conclusão da fala e processa a próxima na fila
    /// </summary>
    private IEnumerator WaitForSpeechCompletion(float duration)
    {
        yield return new WaitForSeconds(duration);
        
        isSpeaking = false;
        OnSpeechCompleted?.Invoke();
        
        if (enableDebugLogs)
            Debug.Log("Fala concluída");
        
        // Processa próximo item da fila se houver
        if (speechQueue.Count > 0)
        {
            string nextText = speechQueue.Dequeue();
            await Speak(nextText);
        }
    }
    
    /// <summary>
    /// Adiciona áudio ao cache, removendo o mais antigo se necessário
    /// </summary>
    private void AddToCache(string text, AudioClip clip)
    {
        if (audioCache.Count >= maxCacheSize)
        {
            // Remove o primeiro item (mais antigo)
            var firstKey = "";
            foreach (var key in audioCache.Keys)
            {
                firstKey = key;
                break;
            }
            
            if (!string.IsNullOrEmpty(firstKey))
            {
                audioCache.Remove(firstKey);
            }
        }
        
        audioCache[text] = clip;
        
        if (enableDebugLogs)
            Debug.Log($"Áudio adicionado ao cache. Tamanho do cache: {audioCache.Count}");
    }
    
    /// <summary>
    /// Lida com fallbacks quando o TTS falha
    /// </summary>
    private void HandleTTSFallback(string text)
    {
        if (enableDebugLogs)
            Debug.Log($"Usando fallback para: {text}");
        
        // Tenta tocar um som fallback aleatório
        if (fallbackSounds != null && fallbackSounds.Length > 0)
        {
            var randomSound = fallbackSounds[UnityEngine.Random.Range(0, fallbackSounds.Length)];
            if (randomSound != null)
            {
                PlayAudioClip(randomSound);
                return;
            }
        }
        
        // Se não tem sons fallback, apenas marca como concluído
        OnSpeechCompleted?.Invoke();
    }
    
    // Métodos públicos para controle externo
    
    /// <summary>
    /// Para a fala atual
    /// </summary>
    public void StopSpeaking()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        isSpeaking = false;
        
        // Limpa a fila se necessário
        speechQueue.Clear();
        
        OnSpeechCompleted?.Invoke();
    }
    
    /// <summary>
    /// Verifica se está atualmente falando
    /// </summary>
    public bool IsSpeaking()
    {
        return isSpeaking || (audioSource != null && audioSource.isPlaying);
    }
    
    /// <summary>
    /// Verifica se está gerando áudio
    /// </summary>
    public bool IsGenerating()
    {
        return isGenerating;
    }
    
    /// <summary>
    /// Limpa o cache de áudios
    /// </summary>
    public void ClearCache()
    {
        audioCache.Clear();
        if (enableDebugLogs)
            Debug.Log("Cache de áudio limpo");
    }
    
    /// <summary>
    /// Retorna quantos itens estão na fila de fala
    /// </summary>
    public int GetQueueSize()
    {
        return speechQueue.Count;
    }
    
    /// <summary>
    /// Método de conveniência para falar respostas da IA
    /// </summary>
    public async void SpeakAIResponse(string response)
    {
        if (!string.IsNullOrEmpty(response))
        {
            await Speak(response);
        }
    }
    
    /// <summary>
    /// Ajusta a velocidade da fala em tempo real
    /// </summary>
    public void SetSpeechSpeed(float speed)
    {
        speechSpeed = Mathf.Clamp(speed, 0.25f, 4.0f);
        
        if (enableDebugLogs)
            Debug.Log($"Velocidade da fala ajustada para: {speechSpeed}");
    }
    
    /// <summary>
    /// Muda a voz utilizada
    /// </summary>
    public void SetVoice(string newVoiceName)
    {
        voiceName = newVoiceName;
        
        if (enableDebugLogs)
            Debug.Log($"Voz alterada para: {voiceName}");
    }
}