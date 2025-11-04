using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gerenciador de respostas de IA que envia texto para modelos de linguagem
/// Usa prompts seguros para crianças e retorna respostas curtas e adequadas
/// Suporta OpenAI GPT, APIs customizadas e fallbacks offline
/// </summary>
public class AIResponder : MonoBehaviour
{
    [Header("Configurações da API")]
    [SerializeField] private string apiEndpoint = "https://api.openai.com/v1/chat/completions"; // URL da API de chat
    [SerializeField] private string configFileName = "config.json"; // Arquivo de configuração em StreamingAssets
    [SerializeField] private float requestTimeoutSeconds = 30f; // Tempo limite para requisições
    [SerializeField] private int maxResponseLength = 100; // Tamanho máximo da resposta em caracteres
    
    [Header("Configurações de Prompt")]
    [SerializeField] private string systemPrompt = "Você é um amigo virtual fofo chamado Criamigo. Responda sempre de forma amigável, simples e segura para crianças. Use no máximo 2 frases curtas. Seja carinhoso e educativo.";
    [SerializeField] private string[] fallbackResponses = { // Respostas padrão quando a API falha
        "Oi! Como você está?",
        "Que legal! Me conte mais!",
        "Vamos brincar juntos?",
        "Você é muito especial!",
        "Estou aqui para ser seu amigo!"
    };
    
    [Header("Configurações de Segurança")]
    [SerializeField] private string[] blockedWords = { // Palavras que devem ser filtradas
        "violência", "medo", "assustador", "perigoso"
    };
    [SerializeField] private bool enableContentFilter = true; // Ativa filtro de conteúdo
    [SerializeField] private bool enableDebugLogs = true; // Ativa logs detalhados
    
    // Eventos para outros sistemas escutarem
    public event Action<string> OnResponseReceived; // Disparado quando recebe resposta da IA
    public event Action<string> OnError; // Disparado quando ocorre erro
    public event Action OnRequestStarted; // Disparado quando inicia uma requisição
    public event Action OnRequestCompleted; // Disparado quando completa uma requisição
    
    // Estado interno
    private string apiKey = ""; // Chave da API carregada do arquivo de configuração
    private bool isProcessing = false; // Flag que indica se está processando uma requisição
    private System.Random random = new System.Random(); // Para escolher respostas fallback aleatórias
    
    // Configuração carregada do arquivo JSON
    [System.Serializable]
    private class ConfigData
    {
        public string openai_api_key; // Chave da API OpenAI
        public string ai_endpoint; // Endpoint personalizado (opcional)
        public string ai_model; // Modelo a usar (ex: "gpt-3.5-turbo")
        public float ai_temperature; // Temperatura para controlar criatividade (0-1)
    }
    
    void Start()
    {
        // Carrega configurações do arquivo StreamingAssets
        LoadConfiguration();
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
                Debug.Log($"Carregando configuração de: {configPath}");
            
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
                    Debug.LogError($"Erro ao carregar configuração: {www.error}");
                    OnError?.Invoke("Arquivo de configuração não encontrado");
                    return;
                }
                
                // Faz parse do JSON
                ConfigData config = JsonUtility.FromJson<ConfigData>(www.downloadHandler.text);
                apiKey = config.openai_api_key;
                
                // Usa endpoint customizado se especificado
                if (!string.IsNullOrEmpty(config.ai_endpoint))
                    apiEndpoint = config.ai_endpoint;
                
                if (enableDebugLogs)
                    Debug.Log("Configurações de IA carregadas com sucesso!");
                
                // Verifica se a chave foi carregada
                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.LogWarning("Chave da API não configurada. Usando apenas respostas fallback.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao carregar configuração: {e.Message}");
            OnError?.Invoke("Erro na configuração da IA");
        }
    }
    
    /// <summary>
    /// Envia uma mensagem para a IA e retorna a resposta
    /// </summary>
    public async Task<string> RequestAIResponse(string userMessage)
    {
        if (isProcessing)
        {
            Debug.LogWarning("Já está processando uma requisição de IA!");
            return GetFallbackResponse();
        }
        
        if (string.IsNullOrEmpty(userMessage))
        {
            Debug.LogWarning("Mensagem vazia enviada para IA!");
            return GetFallbackResponse();
        }
        
        // Filtra conteúdo antes de enviar
        if (enableContentFilter && ContainsBlockedContent(userMessage))
        {
            if (enableDebugLogs)
                Debug.Log("Mensagem contém conteúdo bloqueado, usando resposta segura.");
            return GetSafeResponse();
        }
        
        isProcessing = true;
        OnRequestStarted?.Invoke();
        
        try
        {
            if (enableDebugLogs)
                Debug.Log($"Enviando para IA: {userMessage}");
            
            string response;
            
            // Se não tem chave de API, usa fallback
            if (string.IsNullOrEmpty(apiKey))
            {
                response = GetFallbackResponse();
            }
            else
            {
                // Tenta usar a API real
                response = await SendToAIAPI(userMessage);
                
                // Se falhou, usa fallback
                if (string.IsNullOrEmpty(response))
                {
                    response = GetFallbackResponse();
                }
            }
            
            // Processa e filtra a resposta
            response = ProcessAIResponse(response);
            
            if (enableDebugLogs)
                Debug.Log($"Resposta da IA: {response}");
            
            OnResponseReceived?.Invoke(response);
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro na requisição de IA: {e.Message}");
            OnError?.Invoke($"Erro na IA: {e.Message}");
            return GetFallbackResponse();
        }
        finally
        {
            isProcessing = false;
            OnRequestCompleted?.Invoke();
        }
    }
    
    /// <summary>
    /// Envia a mensagem para a API da IA usando async/await
    /// </summary>
    private async Task<string> SendToAIAPI(string message)
    {
        // Constrói o payload JSON para a API
        var requestData = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = message }
            },
            max_tokens = maxResponseLength,
            temperature = 0.7f,
            stream = false
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
                Debug.LogError($"Erro na API de IA: {request.error}");
                if (request.responseCode == 401)
                {
                    Debug.LogError("Erro de autenticação: verifique sua chave de API");
                }
                return null;
            }
            
            // Faz parse da resposta
            try
            {
                AIResponse response = JsonUtility.FromJson<AIResponse>(request.downloadHandler.text);
                
                if (response.choices != null && response.choices.Length > 0)
                {
                    return response.choices[0].message.content;
                }
                else
                {
                    Debug.LogError("Resposta da IA não contém choices válidos");
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Erro ao fazer parse da resposta da IA: {e.Message}");
                return null;
            }
        }
    }
    
    /// <summary>
    /// Estruturas para fazer parse da resposta da API OpenAI
    /// </summary>
    [System.Serializable]
    private class AIResponse
    {
        public Choice[] choices;
    }
    
    [System.Serializable]
    private class Choice
    {
        public Message message;
    }
    
    [System.Serializable]
    private class Message
    {
        public string content;
    }
    
    /// <summary>
    /// Processa a resposta da IA para garantir que é segura e adequada
    /// </summary>
    private string ProcessAIResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return GetFallbackResponse();
        
        // Remove quebras de linha excessivas
        response = response.Trim();
        
        // Limita o tamanho da resposta
        if (response.Length > maxResponseLength)
        {
            response = response.Substring(0, maxResponseLength);
            
            // Tenta cortar na última frase completa
            int lastPeriod = response.LastIndexOf('.');
            if (lastPeriod > 0 && lastPeriod > maxResponseLength / 2)
            {
                response = response.Substring(0, lastPeriod + 1);
            }
            else
            {
                response += "...";
            }
        }
        
        // Filtra conteúdo inadequado
        if (enableContentFilter && ContainsBlockedContent(response))
        {
            return GetSafeResponse();
        }
        
        return response;
    }
    
    /// <summary>
    /// Verifica se o texto contém palavras bloqueadas
    /// </summary>
    private bool ContainsBlockedContent(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        
        string lowerText = text.ToLower();
        
        foreach (string blockedWord in blockedWords)
        {
            if (lowerText.Contains(blockedWord.ToLower()))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Retorna uma resposta fallback aleatória quando a API falha
    /// </summary>
    private string GetFallbackResponse()
    {
        if (fallbackResponses.Length == 0)
            return "Oi! Como posso ajudar você?";
        
        int randomIndex = random.Next(fallbackResponses.Length);
        return fallbackResponses[randomIndex];
    }
    
    /// <summary>
    /// Retorna uma resposta segura quando conteúdo inadequado é detectado
    /// </summary>
    private string GetSafeResponse()
    {
        string[] safeResponses = {
            "Vamos falar de coisas legais e divertidas!",
            "Que tal me contar sobre algo que você gosta?",
            "Prefiro conversar sobre coisas felizes!",
            "Vamos brincar e aprender coisas novas!"
        };
        
        int randomIndex = random.Next(safeResponses.Length);
        return safeResponses[randomIndex];
    }
    
    /// <summary>
    /// Método de conveniência para perguntas simples à IA
    /// </summary>
    public async void AskQuestion(string question)
    {
        string response = await RequestAIResponse(question);
        // A resposta será enviada através do evento OnResponseReceived
    }
    
    /// <summary>
    /// Método para fazer a criatura reagir com IA baseado em seu estado
    /// </summary>
    public async void ReactToCreatureState(bool isHungry, bool isTired, bool isSad)
    {
        string stateMessage = "Como você está se sentindo?";
        
        if (isHungry)
            stateMessage = "Estou com fome! Você pode me ajudar?";
        else if (isTired)
            stateMessage = "Estou um pouco cansado. O que podemos fazer?";
        else if (isSad)
            stateMessage = "Estou meio tristinho. Pode me animar?";
        
        await RequestAIResponse(stateMessage);
    }
    
    /// <summary>
    /// Retorna se está processando uma requisição
    /// </summary>
    public bool IsProcessing()
    {
        return isProcessing;
    }
    
    /// <summary>
    /// Adiciona uma nova resposta fallback
    /// </summary>
    public void AddFallbackResponse(string response)
    {
        if (!string.IsNullOrEmpty(response))
        {
            Array.Resize(ref fallbackResponses, fallbackResponses.Length + 1);
            fallbackResponses[fallbackResponses.Length - 1] = response;
        }
    }
    
    /// <summary>
    /// Método para testar a conexão com a IA
    /// </summary>
    public async Task<bool> TestConnection()
    {
        try
        {
            string testResponse = await RequestAIResponse("Oi, você está funcionando?");
            return !string.IsNullOrEmpty(testResponse);
        }
        catch
        {
            return false;
        }
    }
}