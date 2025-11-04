using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gerenciador de Speech-to-Text que captura áudio do microfone e envia para API de transcrição
/// Usa UnityWebRequest com async/await para comunicação com serviços STT
/// Suporta gravação contínua e por tempo determinado
/// </summary>
public class SpeechToTextManager : MonoBehaviour
{
    [Header("Configurações de Gravação")]
    [SerializeField] private string microphoneName = null; // Nome do microfone (null = padrão)
    [SerializeField] private int recordingLength = 10; // Duração máxima de gravação em segundos
    [SerializeField] private int sampleRate = 44100; // Taxa de amostragem do áudio
    [SerializeField] private float recordingVolumeThreshold = 0.01f; // Limite mínimo de volume para considerar áudio válido
    
    [Header("Configurações da API")]
    [SerializeField] private string apiEndpoint = "https://api.openai.com/v1/audio/transcriptions"; // URL da API de STT
    [SerializeField] private string configFileName = "config.json"; // Nome do arquivo de configuração em StreamingAssets
    [SerializeField] private float requestTimeoutSeconds = 30f; // Tempo limite para requisições HTTP
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true; // Ativa logs detalhados para debug
    
    // Eventos para outros sistemas escutarem
    public event Action OnRecordingStarted; // Disparado quando inicia a gravação
    public event Action OnRecordingStopped; // Disparado quando para a gravação  
    public event Action<string> OnTranscriptionReceived; // Disparado quando recebe texto transcrito
    public event Action<string> OnError; // Disparado quando ocorre erro
    
    // Estado interno do sistema
    private AudioClip recordedClip; // Clip de áudio gravado
    private bool isRecording = false; // Flag que indica se está gravando
    private bool isProcessing = false; // Flag que indica se está processando a transcrição
    private string apiKey = ""; // Chave da API carregada do arquivo de configuração
    
    // Configuração carregada do arquivo JSON
    [System.Serializable]
    private class ConfigData
    {
        public string openai_api_key; // Chave da API OpenAI para STT
        public string stt_endpoint; // Endpoint personalizado para STT (opcional)
        public string stt_model; // Modelo de STT a usar (ex: "whisper-1")
    }
    
    void Start()
    {
        // Carrega as configurações de API do arquivo StreamingAssets
        StartCoroutine(LoadConfiguration());
        
        // Verifica se há microfones disponíveis no dispositivo
        CheckMicrophoneAvailability();
    }
    
    /// <summary>
    /// Carrega as configurações de API do arquivo config.json em StreamingAssets
    /// </summary>
    private IEnumerator LoadConfiguration()
    {
        // Constrói o caminho para o arquivo de configuração
        string configPath = System.IO.Path.Combine(Application.streamingAssetsPath, configFileName);
        
        if (enableDebugLogs)
            Debug.Log($"Tentando carregar configuração de: {configPath}");
        
        // Usa UnityWebRequest para carregar o arquivo (funciona em todas as plataformas)
        using (UnityWebRequest www = UnityWebRequest.Get(configPath))
        {
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Erro ao carregar arquivo de configuração: {www.error}");
                Debug.LogError("Certifique-se de que o arquivo config.json existe em Assets/StreamingAssets/");
                OnError?.Invoke("Arquivo de configuração não encontrado");
                yield break;
            }
            
            try
            {
                // Faz parse do JSON de configuração
                ConfigData config = JsonUtility.FromJson<ConfigData>(www.downloadHandler.text);
                apiKey = config.openai_api_key;
                
                // Se há endpoint customizado, usa ele
                if (!string.IsNullOrEmpty(config.stt_endpoint))
                    apiEndpoint = config.stt_endpoint;
                
                if (enableDebugLogs)
                    Debug.Log("Configurações carregadas com sucesso!");
                
                // Verifica se a chave foi carregada
                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.LogError("Chave da API não foi encontrada no arquivo de configuração!");
                    OnError?.Invoke("Chave da API não configurada");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Erro ao fazer parse do arquivo de configuração: {e.Message}");
                OnError?.Invoke("Erro no formato do arquivo de configuração");
            }
        }
    }
    
    /// <summary>
    /// Verifica se há microfones disponíveis no sistema
    /// </summary>
    private void CheckMicrophoneAvailability()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("Nenhum microfone encontrado no sistema!");
            OnError?.Invoke("Nenhum microfone disponível");
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"Microfones encontrados: {Microphone.devices.Length}");
            for (int i = 0; i < Microphone.devices.Length; i++)
            {
                Debug.Log($"Microfone {i}: {Microphone.devices[i]}");
            }
        }
    }
    
    /// <summary>
    /// Inicia a gravação de áudio do microfone
    /// </summary>
    public void StartRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning("Já está gravando áudio!");
            return;
        }
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("Não é possível gravar: chave da API não configurada!");
            OnError?.Invoke("API não configurada");
            return;
        }
        
        try
        {
            // Inicia a gravação usando a API do Unity
            recordedClip = Microphone.Start(microphoneName, false, recordingLength, sampleRate);
            isRecording = true;
            
            if (enableDebugLogs)
                Debug.Log("Gravação iniciada...");
            
            OnRecordingStarted?.Invoke(); // Notifica outros sistemas
            
            // Inicia coroutine para monitorar o fim da gravação
            StartCoroutine(MonitorRecording());
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao iniciar gravação: {e.Message}");
            OnError?.Invoke($"Erro na gravação: {e.Message}");
        }
    }
    
    /// <summary>
    /// Para a gravação de áudio
    /// </summary>
    public void StopRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("Não está gravando áudio!");
            return;
        }
        
        // Para o microfone
        Microphone.End(microphoneName);
        isRecording = false;
        
        if (enableDebugLogs)
            Debug.Log("Gravação parada.");
        
        OnRecordingStopped?.Invoke(); // Notifica outros sistemas
        
        // Inicia o processo de transcrição se há áudio válido
        if (recordedClip != null && HasValidAudio(recordedClip))
        {
            ProcessRecordedAudio();
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log("Áudio gravado é muito baixo ou inválido, não será processado.");
        }
    }
    
    /// <summary>
    /// Monitora a gravação e para automaticamente quando atinge o tempo limite
    /// </summary>
    private IEnumerator MonitorRecording()
    {
        float timer = 0f;
        
        while (isRecording && timer < recordingLength)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        
        // Se ainda está gravando após o tempo limite, para automaticamente
        if (isRecording)
        {
            if (enableDebugLogs)
                Debug.Log("Tempo limite de gravação atingido, parando automaticamente...");
            
            StopRecording();
        }
    }
    
    /// <summary>
    /// Verifica se o áudio gravado tem volume suficiente para ser válido
    /// </summary>
    private bool HasValidAudio(AudioClip clip)
    {
        if (clip == null) return false;
        
        // Pega uma amostra do áudio para verificar o volume
        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);
        
        // Calcula o volume médio
        float averageVolume = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            averageVolume += Mathf.Abs(samples[i]);
        }
        averageVolume /= samples.Length;
        
        if (enableDebugLogs)
            Debug.Log($"Volume médio do áudio: {averageVolume}");
        
        return averageVolume > recordingVolumeThreshold;
    }
    
    /// <summary>
    /// Processa o áudio gravado e envia para a API de STT
    /// </summary>
    private async void ProcessRecordedAudio()
    {
        if (isProcessing)
        {
            Debug.LogWarning("Já está processando uma transcrição!");
            return;
        }
        
        isProcessing = true;
        
        try
        {
            if (enableDebugLogs)
                Debug.Log("Enviando áudio para transcrição...");
            
            // Converte o AudioClip para bytes WAV
            byte[] audioBytes = ConvertAudioClipToWav(recordedClip);
            
            // Envia para a API e aguarda o resultado
            string transcription = await SendAudioToAPI(audioBytes);
            
            if (!string.IsNullOrEmpty(transcription))
            {
                if (enableDebugLogs)
                    Debug.Log($"Transcrição recebida: {transcription}");
                
                OnTranscriptionReceived?.Invoke(transcription); // Notifica com o texto transcrito
            }
            else
            {
                OnError?.Invoke("Transcrição vazia ou falhou");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao processar áudio: {e.Message}");
            OnError?.Invoke($"Erro no processamento: {e.Message}");
        }
        finally
        {
            isProcessing = false;
        }
    }
    
    /// <summary>
    /// Converte AudioClip para formato WAV em bytes
    /// </summary>
    private byte[] ConvertAudioClipToWav(AudioClip clip)
    {
        // Pega os dados de áudio do clip
        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);
        
        // Converte para bytes WAV (simplificado)
        byte[] wavBytes = new byte[samples.Length * 2]; // 16-bit = 2 bytes por sample
        
        for (int i = 0; i < samples.Length; i++)
        {
            // Converte float para 16-bit integer
            short intSample = (short)(samples[i] * 32767f);
            byte[] bytes = System.BitConverter.GetBytes(intSample);
            wavBytes[i * 2] = bytes[0];
            wavBytes[i * 2 + 1] = bytes[1];
        }
        
        return wavBytes;
    }
    
    /// <summary>
    /// Envia os bytes de áudio para a API de STT usando async/await
    /// </summary>
    private async Task<string> SendAudioToAPI(byte[] audioBytes)
    {
        // Cria o form data para upload multipart
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioBytes, "audio.wav", "audio/wav");
        form.AddField("model", "whisper-1"); // Usa o modelo Whisper da OpenAI
        form.AddField("language", "pt"); // Português brasileiro
        
        using (UnityWebRequest request = UnityWebRequest.Post(apiEndpoint, form))
        {
            // Adiciona header de autenticação
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.timeout = (int)requestTimeoutSeconds;
            
            // Envia a requisição de forma assíncrona
            var operation = request.SendWebRequest();
            
            // Aguarda a conclusão da requisição
            while (!operation.isDone)
            {
                await Task.Yield(); // Não bloqueia a thread principal
            }
            
            // Verifica se houve erro
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Erro na API de STT: {request.error}");
                Debug.LogError($"Código de resposta: {request.responseCode}");
                if (!string.IsNullOrEmpty(request.downloadHandler.text))
                    Debug.LogError($"Detalhes do erro: {request.downloadHandler.text}");
                return null;
            }
            
            // Faz parse da resposta JSON
            try
            {
                STTResponse response = JsonUtility.FromJson<STTResponse>(request.downloadHandler.text);
                return response.text;
            }
            catch (Exception e)
            {
                Debug.LogError($"Erro ao fazer parse da resposta STT: {e.Message}");
                return null;
            }
        }
    }
    
    /// <summary>
    /// Estrutura para fazer parse da resposta da API de STT
    /// </summary>
    [System.Serializable]
    private class STTResponse
    {
        public string text; // Texto transcrito retornado pela API
    }
    
    // Métodos públicos para controle externo
    
    /// <summary>
    /// Retorna se está atualmente gravando
    /// </summary>
    public bool IsRecording()
    {
        return isRecording;
    }
    
    /// <summary>
    /// Retorna se está processando uma transcrição
    /// </summary>
    public bool IsProcessing()
    {
        return isProcessing;
    }
    
    /// <summary>
    /// Método de conveniência para gravar por um tempo específico
    /// </summary>
    public void RecordForDuration(float seconds)
    {
        if (seconds <= 0 || seconds > recordingLength)
        {
            Debug.LogError($"Duração inválida: {seconds}s. Deve ser entre 0 e {recordingLength}s");
            return;
        }
        
        StartRecording();
        
        // Para automaticamente após o tempo especificado
        StartCoroutine(StopRecordingAfterDelay(seconds));
    }
    
    /// <summary>
    /// Coroutine para parar gravação após delay
    /// </summary>
    private IEnumerator StopRecordingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (isRecording)
            StopRecording();
    }
}