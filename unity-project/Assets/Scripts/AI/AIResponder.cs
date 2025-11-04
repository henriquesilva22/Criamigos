using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Criamigos.AI
{
    /// <summary>
    /// Sistema de IA responsável por gerar respostas seguras e apropriadas para crianças
    /// Conecta-se com APIs de linguagem (OpenAI) e possui sistema robusto de fallbacks offline
    /// Filtra conteúdo inadequado e mantém conversas sempre positivas e educativas
    /// </summary>
    public class AIResponder : MonoBehaviour
    {
        [Header("Configurações da API")]
        [SerializeField] private string apiEndpoint = "https://api.openai.com/v1/chat/completions"; // URL da API de chat
        [SerializeField] private float requestTimeoutSeconds = 15f; // Tempo limite para requisições (reduzido para crianças)
        [SerializeField] private int maxResponseTokens = 50; // Máximo de tokens para respostas curtas
        
        [Header("Configurações de Segurança Infantil")]
        [SerializeField] private string childSafePrompt = "Você é um Criamigo, um pet virtual fofo e carinhoso. Responda SEMPRE de forma muito simples, positiva e segura para crianças pequenas. Use no máximo 1-2 frases curtas. Seja sempre alegre, educativo e encorajador. Nunca mencione tópicos adultos, assustadores ou negativos.";
        [SerializeField] private string[] blockedKeywords = { // Palavras que são bloqueadas automaticamente
            "violência", "medo", "triste", "machucado", "perigoso", "ruim", "morte",
            "violence", "scary", "sad", "hurt", "dangerous", "bad", "death"
        };
        [SerializeField] private bool enableStrictFiltering = true; // Ativa filtro rigoroso para crianças
        
        [Header("Respostas Fallback Offline")]
        [SerializeField] private string[] happyResponses = { // Respostas alegres quando offline
            "Que legal! Você é incrível! 🌟",
            "Nossa, que divertido! Vamos brincar mais! 🎮",
            "Você é meu melhor amigo! 💖",
            "Eba! Adoro quando você fala comigo! 😄",
            "Que maravilha! Estou muito feliz! ✨"
        };
        [SerializeField] private string[] encouragingResponses = { // Respostas encorajadoras
            "Você é muito especial! Continue assim! 🌈",
            "Que orgulho de você! 🏆",
            "Você consegue! Acredito em você! 💪",
            "Que inteligente você é! 🧠✨",
            "Adoro aprender coisas novas com você! 📚"
        };
        [SerializeField] private string[] needsResponses = { // Respostas quando tem necessidades
            "Estou com um pouquinho de fome... pode me ajudar? 🍎",
            "Que tal brincarmos juntos? Estou querendo me divertir! 🎯",
            "Estou meio cansadinho... posso descansar um pouco? 😴",
            "Você pode cuidar de mim? Preciso de carinho! 🤗"
        };
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true; // Ativa logs para desenvolvimento
        [SerializeField] private bool simulateOfflineMode = false; // Força modo offline para testes
        
        // Eventos públicos para outros sistemas escutarem
        public event Action<string> OnResponseReceived; // Disparado quando recebe resposta válida
        public event Action<string> OnError; // Disparado quando ocorre erro
        public event Action OnRequestStarted; // Disparado quando inicia uma requisição
        public event Action OnRequestCompleted; // Disparado quando completa (sucesso ou erro)
        
        // Estado interno do sistema
        private bool isProcessingRequest = false; // Controla se está processando uma requisição
        private string apiKey = ""; // Chave da API carregada dinamicamente
        private System.Random randomGenerator; // Gerador de números aleatórios para fallbacks
        private Queue<string> conversationHistory; // Histórico das últimas conversas
        private int maxHistorySize = 5; // Máximo de mensagens no histórico
        
        // Cache de respostas para perguntas similares
        private Dictionary<string, string> responseCache;
        private int maxCacheSize = 20;
        
        void Awake()
        {
            // Inicializa componentes internos
            randomGenerator = new System.Random();
            conversationHistory = new Queue<string>();
            responseCache = new Dictionary<string, string>();
        }
        
        void Start()
        {
            // Carrega configurações da API de forma assíncrona
            StartCoroutine(LoadAPIConfiguration());
            
            if (enableDebugLogs)
                Debug.Log("[AIResponder] Sistema iniciado - Modo seguro para crianças ativado");
        }
        
        /// <summary>
        /// Carrega a chave da API do sistema de configuração
        /// </summary>
        private IEnumerator LoadAPIConfiguration()
        {
            // Tenta encontrar o ConfigLoader na cena
            var configLoader = FindObjectOfType<Core.ConfigLoader>();
            if (configLoader != null)
            {
                // Aguarda o ConfigLoader carregar as configurações
                yield return new WaitUntil(() => configLoader.IsConfigLoaded());
                
                // Obtém a chave da API
                apiKey = configLoader.GetAPIKey("openai");
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.LogWarning("[AIResponder] Chave da API não encontrada - usando apenas modo offline");
                }
                else if (enableDebugLogs)
                {
                    Debug.Log("[AIResponder] Configuração carregada com sucesso");
                }
            }
            else
            {
                Debug.LogWarning("[AIResponder] ConfigLoader não encontrado - usando apenas modo offline");
            }
        }
        
        /// <summary>
        /// Método principal para solicitar uma resposta da IA
        /// </summary>
        /// <param name="userMessage">Mensagem enviada pela criança</param>
        /// <param name="context">Contexto adicional (estado da criatura, etc.)</param>
        public void RequestResponse(string userMessage, string context = "")
        {
            // Valida entrada
            if (string.IsNullOrEmpty(userMessage))
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[AIResponder] Mensagem vazia recebida");
                
                HandleResponse(GetRandomFallbackResponse());
                return;
            }
            
            // Verifica se já está processando
            if (isProcessingRequest)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[AIResponder] Já processando uma requisição - ignorando nova");
                return;
            }
            
            // Filtra conteúdo antes de processar
            if (enableStrictFiltering && ContainsInappropriateContent(userMessage))
            {
                if (enableDebugLogs)
                    Debug.Log("[AIResponder] Conteúdo filtrado - usando resposta segura");
                
                HandleResponse(GetSafeRedirectResponse());
                return;
            }
            
            // Verifica cache primeiro
            string cachedResponse = GetCachedResponse(userMessage);
            if (!string.IsNullOrEmpty(cachedResponse))
            {
                if (enableDebugLogs)
                    Debug.Log("[AIResponder] Usando resposta do cache");
                
                HandleResponse(cachedResponse);
                return;
            }
            
            // Decide se usa API ou fallback
            if (ShouldUseAPI())
            {
                StartCoroutine(ProcessAPIRequest(userMessage, context));
            }
            else
            {
                HandleResponse(GetContextualFallbackResponse(userMessage, context));
            }
        }
        
        /// <summary>
        /// Verifica se deve tentar usar a API ou ir direto para fallback
        /// </summary>
        private bool ShouldUseAPI()
        {
            return !simulateOfflineMode && 
                   !string.IsNullOrEmpty(apiKey) && 
                   Application.internetReachability != NetworkReachability.NotReachable;
        }
        
        /// <summary>
        /// Processa requisição para a API de forma assíncrona
        /// </summary>
        private IEnumerator ProcessAPIRequest(string message, string context)
        {
            isProcessingRequest = true;
            OnRequestStarted?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log($"[AIResponder] Enviando para API: {message}");
            
            // Constrói o prompt seguro para crianças
            string enhancedPrompt = BuildChildSafePrompt(message, context);
            
            // Cria o payload JSON para OpenAI
            var requestData = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = childSafePrompt },
                    new { role = "user", content = enhancedPrompt }
                },
                max_tokens = maxResponseTokens,
                temperature = 0.7f, // Criatividade moderada mas controlada
                presence_penalty = 0.2f, // Evita repetição
                frequency_penalty = 0.3f
            };
            
            string jsonPayload = JsonUtility.ToJson(requestData);
            
            using (UnityWebRequest request = new UnityWebRequest(apiEndpoint, "POST"))
            {
                // Configura o request
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                request.timeout = (int)requestTimeoutSeconds;
                
                // Envia requisição
                yield return request.SendWebRequest();
                
                // Processa resultado
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        // Faz parse da resposta JSON
                        var responseJson = JsonUtility.FromJson<OpenAIResponse>(request.downloadHandler.text);
                        
                        if (responseJson.choices != null && responseJson.choices.Length > 0)
                        {
                            string aiResponse = responseJson.choices[0].message.content.Trim();
                            
                            // Filtra e processa a resposta antes de enviar
                            aiResponse = ProcessAIResponse(aiResponse);
                            
                            // Adiciona ao cache
                            CacheResponse(message, aiResponse);
                            
                            HandleResponse(aiResponse);
                        }
                        else
                        {
                            throw new Exception("Resposta da API sem conteúdo válido");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[AIResponder] Erro ao processar resposta da API: {e.Message}");
                        HandleResponse(GetRandomFallbackResponse());
                    }
                }
                else
                {
                    Debug.LogWarning($"[AIResponder] Erro na API ({request.responseCode}): {request.error}");
                    HandleResponse(GetRandomFallbackResponse());
                }
            }
            
            isProcessingRequest = false;
            OnRequestCompleted?.Invoke();
        }
        
        /// <summary>
        /// Constrói um prompt seguro e contextualizado para crianças
        /// </summary>
        private string BuildChildSafePrompt(string message, string context)
        {
            string prompt = message;
            
            // Adiciona contexto se fornecido
            if (!string.IsNullOrEmpty(context))
            {
                prompt = $"Contexto da criatura: {context}. Mensagem da criança: {message}";
            }
            
            // Adiciona histórico recente para continuidade
            if (conversationHistory.Count > 0)
            {
                string history = string.Join(", ", conversationHistory.ToArray());
                prompt += $"\nTópicos recentes: {history}";
            }
            
            return prompt;
        }
        
        /// <summary>
        /// Processa e filtra a resposta da IA antes de entregar
        /// </summary>
        private string ProcessAIResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return GetRandomFallbackResponse();
            
            // Remove possíveis caracteres problemáticos
            response = response.Replace("\"", "").Replace("\n", " ").Trim();
            
            // Limita o tamanho da resposta
            if (response.Length > 100)
            {
                response = response.Substring(0, 97) + "...";
            }
            
            // Filtra novamente por segurança
            if (enableStrictFiltering && ContainsInappropriateContent(response))
            {
                return GetSafeRedirectResponse();
            }
            
            // Adiciona emoji se não tiver
            if (!ContainsEmoji(response))
            {
                response += " " + GetRandomEmoji();
            }
            
            return response;
        }
        
        /// <summary>
        /// Verifica se o conteúdo contém palavras ou temas inapropriados
        /// </summary>
        private bool ContainsInappropriateContent(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            string lowerText = text.ToLower();
            
            // Verifica palavras bloqueadas
            foreach (string keyword in blockedKeywords)
            {
                if (lowerText.Contains(keyword.ToLower()))
                {
                    return true;
                }
            }
            
            // Verificações adicionais para temas adultos
            string[] adultThemes = { "politics", "religion", "money", "work", "política", "religião", "dinheiro", "trabalho" };
            foreach (string theme in adultThemes)
            {
                if (lowerText.Contains(theme))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Retorna uma resposta segura quando conteúdo inapropriado é detectado
        /// </summary>
        private string GetSafeRedirectResponse()
        {
            string[] safeRedirects = {
                "Vamos falar de coisas divertidas! O que você gosta de brincar? 🎈",
                "Prefiro conversas alegres! Me conta uma coisa boa do seu dia! ☀️",
                "Que tal falarmos sobre cores, animais ou brincadeiras? 🌈",
                "Adoro quando falamos de coisas felizes! Qual sua comida favorita? 🍎"
            };
            
            return safeRedirects[randomGenerator.Next(safeRedirects.Length)];
        }
        
        /// <summary>
        /// Obtém uma resposta contextual baseada no estado da criatura
        /// </summary>
        private string GetContextualFallbackResponse(string message, string context)
        {
            // Se o contexto indica necessidades específicas
            if (!string.IsNullOrEmpty(context))
            {
                if (context.Contains("hungry"))
                    return needsResponses[0];
                if (context.Contains("tired"))
                    return needsResponses[2];
                if (context.Contains("sad"))
                    return needsResponses[3];
            }
            
            // Analisa a mensagem para dar resposta apropriada
            string lowerMessage = message.ToLower();
            
            if (lowerMessage.Contains("olá") || lowerMessage.Contains("oi") || lowerMessage.Contains("hello"))
            {
                return "Oi! Que alegria te ver! Como você está hoje? 😊";
            }
            
            if (lowerMessage.Contains("obrigado") || lowerMessage.Contains("thanks"))
            {
                return "De nada! Estou aqui para ser seu amigo! 💖";
            }
            
            if (lowerMessage.Contains("brinca") || lowerMessage.Contains("joga") || lowerMessage.Contains("play"))
            {
                return "Eba! Adoro brincar! Vamos nos divertir juntos! 🎮";
            }
            
            // Resposta padrão alegre
            return GetRandomFallbackResponse();
        }
        
        /// <summary>
        /// Retorna uma resposta aleatória das listas de fallback
        /// </summary>
        private string GetRandomFallbackResponse()
        {
            // Combina todas as respostas positivas
            List<string> allResponses = new List<string>();
            allResponses.AddRange(happyResponses);
            allResponses.AddRange(encouragingResponses);
            
            return allResponses[randomGenerator.Next(allResponses.Count)];
        }
        
        /// <summary>
        /// Gerencia o cache de respostas para perguntas similares
        /// </summary>
        private void CacheResponse(string question, string response)
        {
            if (responseCache.Count >= maxCacheSize)
            {
                // Remove o item mais antigo (implementação simples)
                var firstKey = "";
                foreach (var key in responseCache.Keys)
                {
                    firstKey = key;
                    break;
                }
                responseCache.Remove(firstKey);
            }
            
            responseCache[question.ToLower()] = response;
        }
        
        /// <summary>
        /// Obtém resposta do cache se disponível
        /// </summary>
        private string GetCachedResponse(string question)
        {
            responseCache.TryGetValue(question.ToLower(), out string cachedResponse);
            return cachedResponse;
        }
        
        /// <summary>
        /// Verifica se o texto contém emojis
        /// </summary>
        private bool ContainsEmoji(string text)
        {
            return text.Contains("😊") || text.Contains("🎮") || text.Contains("💖") || 
                   text.Contains("🌟") || text.Contains("✨") || text.Contains("🌈");
        }
        
        /// <summary>
        /// Retorna um emoji aleatório apropriado para crianças
        /// </summary>
        private string GetRandomEmoji()
        {
            string[] childFriendlyEmojis = { "😊", "🌟", "💖", "🎈", "🌈", "✨", "🎮", "🍎", "🎯", "🏆" };
            return childFriendlyEmojis[randomGenerator.Next(childFriendlyEmojis.Length)];
        }
        
        /// <summary>
        /// Manipula a resposta final e notifica outros sistemas
        /// </summary>
        private void HandleResponse(string response)
        {
            // Adiciona ao histórico de conversa
            conversationHistory.Enqueue(response);
            if (conversationHistory.Count > maxHistorySize)
            {
                conversationHistory.Dequeue();
            }
            
            if (enableDebugLogs)
                Debug.Log($"[AIResponder] Resposta enviada: {response}");
            
            // Notifica outros sistemas
            OnResponseReceived?.Invoke(response);
        }
        
        // Métodos públicos para controle externo
        
        /// <summary>
        /// Verifica se o sistema está processando uma requisição
        /// </summary>
        public bool IsProcessing()
        {
            return isProcessingRequest;
        }
        
        /// <summary>
        /// Limpa o cache de respostas
        /// </summary>
        public void ClearCache()
        {
            responseCache.Clear();
            if (enableDebugLogs)
                Debug.Log("[AIResponder] Cache limpo");
        }
        
        /// <summary>
        /// Força modo offline para testes
        /// </summary>
        public void SetOfflineMode(bool offline)
        {
            simulateOfflineMode = offline;
            if (enableDebugLogs)
                Debug.Log($"[AIResponder] Modo offline: {offline}");
        }
        
        /// <summary>
        /// Estrutura para fazer parse da resposta da API OpenAI
        /// </summary>
        [System.Serializable]
        private class OpenAIResponse
        {
            public Choice[] choices;
            
            [System.Serializable]
            public class Choice
            {
                public Message message;
                
                [System.Serializable]
                public class Message
                {
                    public string content;
                }
            }
        }
    }
}