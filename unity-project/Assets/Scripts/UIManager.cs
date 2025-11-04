using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gerenciador de Interface do Usuário que conecta todos os sistemas visuais
/// Controla barras de progresso, botões de ação e feedback visual para o jogador
/// Atualiza em tempo real baseado nos eventos dos outros sistemas
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Referências de Sistemas")]
    [SerializeField] private HungerSystem hungerSystem; // Sistema de fome/energia/felicidade
    [SerializeField] private CreatureController creatureController; // Controlador da criatura
    [SerializeField] private SpeechToTextManager sttManager; // Gerenciador de speech-to-text
    [SerializeField] private AIResponder aiResponder; // Sistema de respostas de IA
    [SerializeField] private TextToSpeechManager ttsManager; // Gerenciador de text-to-speech
    
    [Header("Barras de Status")]
    [SerializeField] private Slider hungerBar; // Barra visual da fome
    [SerializeField] private Slider energyBar; // Barra visual da energia
    [SerializeField] private Slider happinessBar; // Barra visual da felicidade
    [SerializeField] private TextMeshProUGUI hungerText; // Texto com valor da fome
    [SerializeField] private TextMeshProUGUI energyText; // Texto com valor da energia
    [SerializeField] private TextMeshProUGUI happinessText; // Texto com valor da felicidade
    
    [Header("Botões de Ação")]
    [SerializeField] private Button feedButton; // Botão para alimentar
    [SerializeField] private Button playButton; // Botão para brincar
    [SerializeField] private Button restButton; // Botão para descansar
    [SerializeField] private Button talkButton; // Botão para falar/gravar
    [SerializeField] private Button resetButton; // Botão para resetar tudo (debug)
    
    [Header("Interface de Conversa")]
    [SerializeField] private TextMeshProUGUI conversationText; // Área de texto da conversa
    [SerializeField] private TextMeshProUGUI statusText; // Texto de status (gravando, processando, etc.)
    [SerializeField] private ScrollRect conversationScroll; // Scroll da conversa
    [SerializeField] private GameObject recordingIndicator; // Indicador visual de gravação
    
    [Header("Configurações Visuais")]
    [SerializeField] private Color normalColor = Color.green; // Cor quando valores estão normais
    [SerializeField] private Color warningColor = Color.yellow; // Cor quando valores estão baixos
    [SerializeField] private Color criticalColor = Color.red; // Cor quando valores estão críticos
    [SerializeField] private float warningThreshold = 0.5f; // Limite para cor de aviso
    [SerializeField] private float criticalThreshold = 0.3f; // Limite para cor crítica
    
    [Header("Configurações de Animação")]
    [SerializeField] private bool enableBarAnimations = true; // Ativa animações suaves das barras
    [SerializeField] private float animationSpeed = 2f; // Velocidade das animações
    [SerializeField] private bool enablePulseEffect = true; // Ativa efeito de pulsação em estado crítico
    
    // Variáveis de controle interno
    private bool isRecording = false; // Controla se está gravando áudio
    private bool isProcessing = false; // Controla se está processando IA/TTS
    private string conversationHistory = ""; // Histórico da conversa
    private float targetHungerValue = 1f; // Valor alvo para animação suave da barra de fome
    private float targetEnergyValue = 1f; // Valor alvo para animação suave da barra de energia
    private float targetHappinessValue = 1f; // Valor alvo para animação suave da barra de felicidade
    
    void Start()
    {
        // Inicializa a UI com valores padrão
        InitializeUI();
        
        // Se inscreve nos eventos dos sistemas
        SubscribeToEvents();
        
        // Configura os botões
        SetupButtons();
        
        // Atualiza a UI inicial
        UpdateAllBars();
    }
    
    /// <summary>
    /// Inicializa elementos da UI com valores padrão
    /// </summary>
    private void InitializeUI()
    {
        // Configura barras iniciais
        if (hungerBar != null) hungerBar.value = 1f;
        if (energyBar != null) energyBar.value = 1f;
        if (happinessBar != null) happinessBar.value = 1f;
        
        // Configura textos iniciais
        UpdateStatusText("Sistema iniciado - Tudo pronto!");
        
        if (conversationText != null)
            conversationText.text = "Olá! Eu sou seu Criamigo! Como você está hoje?";
        
        // Esconde indicador de gravação
        if (recordingIndicator != null)
            recordingIndicator.SetActive(false);
        
        Debug.Log("UI Manager inicializado com sucesso!");
    }
    
    /// <summary>
    /// Se inscreve nos eventos de todos os sistemas
    /// </summary>
    private void SubscribeToEvents()
    {
        // Eventos do HungerSystem
        if (hungerSystem != null)
        {
            hungerSystem.OnHungerChanged += UpdateHungerUI;
            hungerSystem.OnEnergyChanged += UpdateEnergyUI;
            hungerSystem.OnHappinessChanged += UpdateHappinessUI;
            hungerSystem.OnCriticalState += HandleCriticalState;
            hungerSystem.OnHealthyState += HandleHealthyState;
        }
        
        // Eventos do SpeechToText
        if (sttManager != null)
        {
            sttManager.OnRecordingStarted += HandleRecordingStarted;
            sttManager.OnRecordingStopped += HandleRecordingStopped;
            sttManager.OnTranscriptionReceived += HandleSTTResult;
            sttManager.OnError += HandleSTTError;
        }
        
        // Eventos da IA
        if (aiResponder != null)
        {
            aiResponder.OnRequestStarted += HandleAIRequestStarted;
            aiResponder.OnResponseReceived += HandleAIResponse;
            aiResponder.OnRequestCompleted += HandleAIRequestCompleted;
            aiResponder.OnError += HandleAIError;
        }
        
        // Eventos do TTS
        if (ttsManager != null)
        {
            ttsManager.OnSpeechStarted += HandleTTSStarted;
            ttsManager.OnSpeechCompleted += HandleTTSCompleted;
            ttsManager.OnError += HandleTTSError;
        }
    }
    
    /// <summary>
    /// Configura os listeners dos botões
    /// </summary>
    private void SetupButtons()
    {
        if (feedButton != null)
            feedButton.onClick.AddListener(OnFeedButtonClicked);
        
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayButtonClicked);
        
        if (restButton != null)
            restButton.onClick.AddListener(OnRestButtonClicked);
        
        if (talkButton != null)
            talkButton.onClick.AddListener(OnTalkButtonClicked);
        
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetButtonClicked);
    }
    
    void Update()
    {
        // Atualiza animações suaves das barras se habilitado
        if (enableBarAnimations)
        {
            UpdateBarAnimations();
        }
        
        // Atualiza efeito de pulsação se habilitado
        if (enablePulseEffect && hungerSystem != null && hungerSystem.IsInCriticalState())
        {
            UpdatePulseEffect();
        }
    }
    
    /// <summary>
    /// Atualiza animações suaves das barras de status
    /// </summary>
    private void UpdateBarAnimations()
    {
        if (hungerBar != null)
        {
            hungerBar.value = Mathf.Lerp(hungerBar.value, targetHungerValue, Time.deltaTime * animationSpeed);
        }
        
        if (energyBar != null)
        {
            energyBar.value = Mathf.Lerp(energyBar.value, targetEnergyValue, Time.deltaTime * animationSpeed);
        }
        
        if (happinessBar != null)
        {
            happinessBar.value = Mathf.Lerp(happinessBar.value, targetHappinessValue, Time.deltaTime * animationSpeed);
        }
    }
    
    /// <summary>
    /// Atualiza efeito visual de pulsação em estado crítico
    /// </summary>
    private void UpdatePulseEffect()
    {
        float pulseValue = Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f; // Oscila entre 0 e 1
        
        // Aplica efeito de pulsação às barras críticas
        if (hungerSystem.GetHungerNormalized() <= criticalThreshold && hungerBar != null)
        {
            hungerBar.transform.localScale = Vector3.one * (1f + pulseValue * 0.1f);
        }
        
        if (hungerSystem.GetEnergyNormalized() <= criticalThreshold && energyBar != null)
        {
            energyBar.transform.localScale = Vector3.one * (1f + pulseValue * 0.1f);
        }
        
        if (hungerSystem.GetHappinessNormalized() <= criticalThreshold && happinessBar != null)
        {
            happinessBar.transform.localScale = Vector3.one * (1f + pulseValue * 0.1f);
        }
    }
    
    // Handlers para eventos do HungerSystem
    
    /// <summary>
    /// Atualiza a UI quando a fome muda
    /// </summary>
    private void UpdateHungerUI(float hungerValue, float normalizedValue)
    {
        targetHungerValue = normalizedValue;
        
        if (!enableBarAnimations && hungerBar != null)
            hungerBar.value = normalizedValue;
        
        if (hungerText != null)
            hungerText.text = $"{Mathf.Round(hungerValue)}/100";
        
        // Atualiza cor baseada no valor
        UpdateBarColor(hungerBar, normalizedValue);
    }
    
    /// <summary>
    /// Atualiza a UI quando a energia muda
    /// </summary>
    private void UpdateEnergyUI(float energyValue, float normalizedValue)
    {
        targetEnergyValue = normalizedValue;
        
        if (!enableBarAnimations && energyBar != null)
            energyBar.value = normalizedValue;
        
        if (energyText != null)
            energyText.text = $"{Mathf.Round(energyValue)}/100";
        
        // Atualiza cor baseada no valor
        UpdateBarColor(energyBar, normalizedValue);
    }
    
    /// <summary>
    /// Atualiza a UI quando a felicidade muda
    /// </summary>
    private void UpdateHappinessUI(float happinessValue, float normalizedValue)
    {
        targetHappinessValue = normalizedValue;
        
        if (!enableBarAnimations && happinessBar != null)
            happinessBar.value = normalizedValue;
        
        if (happinessText != null)
            happinessText.text = $"{Mathf.Round(happinessValue)}/100";
        
        // Atualiza cor baseada no valor
        UpdateBarColor(happinessBar, normalizedValue);
    }
    
    /// <summary>
    /// Atualiza todas as barras de uma vez (útil para inicialização)
    /// </summary>
    private void UpdateAllBars()
    {
        if (hungerSystem != null)
        {
            UpdateHungerUI(hungerSystem.GetHunger(), hungerSystem.GetHungerNormalized());
            UpdateEnergyUI(hungerSystem.GetEnergy(), hungerSystem.GetEnergyNormalized());
            UpdateHappinessUI(hungerSystem.GetHappiness(), hungerSystem.GetHappinessNormalized());
        }
    }
    
    /// <summary>
    /// Atualiza a cor de uma barra baseada no valor
    /// </summary>
    private void UpdateBarColor(Slider bar, float normalizedValue)
    {
        if (bar == null) return;
        
        Color targetColor;
        
        if (normalizedValue <= criticalThreshold)
            targetColor = criticalColor;
        else if (normalizedValue <= warningThreshold)
            targetColor = warningColor;
        else
            targetColor = normalColor;
        
        // Aplica a cor ao fill da barra
        Image fillImage = bar.fillRect.GetComponent<Image>();
        if (fillImage != null)
            fillImage.color = targetColor;
    }
    
    /// <summary>
    /// Chamado quando a criatura entra em estado crítico
    /// </summary>
    private void HandleCriticalState()
    {
        UpdateStatusText("⚠️ Sua criatura precisa de cuidados!");
        AddToConversation("Sistema", "Opa! Estou precisando de cuidados. Pode me ajudar?");
    }
    
    /// <summary>
    /// Chamado quando a criatura sai do estado crítico
    /// </summary>
    private void HandleHealthyState()
    {
        UpdateStatusText("✅ Sua criatura está saudável!");
        AddToConversation("Sistema", "Muito obrigado! Agora estou me sentindo muito melhor!");
    }
    
    // Handlers para eventos de Speech-to-Text
    
    /// <summary>
    /// Chamado quando inicia a gravação
    /// </summary>
    private void HandleRecordingStarted()
    {
        isRecording = true;
        UpdateStatusText("🎙️ Gravando... fale agora!");
        
        if (recordingIndicator != null)
            recordingIndicator.SetActive(true);
        
        // Atualiza aparência do botão de falar
        if (talkButton != null)
        {
            ColorBlock colors = talkButton.colors;
            colors.normalColor = Color.red;
            talkButton.colors = colors;
        }
    }
    
    /// <summary>
    /// Chamado quando para a gravação
    /// </summary>
    private void HandleRecordingStopped()
    {
        isRecording = false;
        UpdateStatusText("⏳ Processando sua fala...");
        
        if (recordingIndicator != null)
            recordingIndicator.SetActive(false);
        
        // Restaura aparência do botão de falar
        if (talkButton != null)
        {
            ColorBlock colors = talkButton.colors;
            colors.normalColor = Color.white;
            talkButton.colors = colors;
        }
    }
    
    /// <summary>
    /// Chamado quando recebe resultado do STT
    /// </summary>
    private void HandleSTTResult(string transcription)
    {
        UpdateStatusText("✅ Te entendi! Pensando numa resposta...");
        AddToConversation("Você", transcription);
        
        // Envia para a IA responder
        if (aiResponder != null)
        {
            aiResponder.AskQuestion(transcription);
        }
    }
    
    /// <summary>
    /// Chamado quando há erro no STT
    /// </summary>
    private void HandleSTTError(string error)
    {
        UpdateStatusText($"❌ Erro no áudio: {error}");
        Debug.LogError($"Erro STT: {error}");
    }
    
    // Handlers para eventos de IA
    
    /// <summary>
    /// Chamado quando inicia requisição para IA
    /// </summary>
    private void HandleAIRequestStarted()
    {
        isProcessing = true;
        UpdateStatusText("🤔 Pensando numa resposta...");
    }
    
    /// <summary>
    /// Chamado quando recebe resposta da IA
    /// </summary>
    private void HandleAIResponse(string response)
    {
        AddToConversation("Criamigo", response);
        
        // Envia para o TTS falar
        if (ttsManager != null)
        {
            ttsManager.SpeakAIResponse(response);
        }
    }
    
    /// <summary>
    /// Chamado quando completa requisição da IA
    /// </summary>
    private void HandleAIRequestCompleted()
    {
        isProcessing = false;
        UpdateStatusText("💬 Pronto para conversar!");
    }
    
    /// <summary>
    /// Chamado quando há erro na IA
    /// </summary>
    private void HandleAIError(string error)
    {
        UpdateStatusText($"❌ Erro na IA: {error}");
        AddToConversation("Sistema", "Desculpe, não consegui entender. Pode tentar novamente?");
        Debug.LogError($"Erro IA: {error}");
    }
    
    // Handlers para eventos de TTS
    
    /// <summary>
    /// Chamado quando inicia a fala
    /// </summary>
    private void HandleTTSStarted(string text)
    {
        UpdateStatusText("🔊 Falando...");
    }
    
    /// <summary>
    /// Chamado quando termina a fala
    /// </summary>
    private void HandleTTSCompleted()
    {
        UpdateStatusText("💬 Pronto para conversar!");
    }
    
    /// <summary>
    /// Chamado quando há erro no TTS
    /// </summary>
    private void HandleTTSError(string error)
    {
        UpdateStatusText($"❌ Erro na voz: {error}");
        Debug.LogError($"Erro TTS: {error}");
    }
    
    // Handlers para cliques dos botões
    
    /// <summary>
    /// Chamado quando clica no botão de alimentar
    /// </summary>
    private void OnFeedButtonClicked()
    {
        if (creatureController != null)
        {
            creatureController.FeedCreature();
            AddToConversation("Ação", "Você alimentou seu Criamigo! 🍎");
            UpdateStatusText("😋 Que delícia! Obrigado!");
        }
    }
    
    /// <summary>
    /// Chamado quando clica no botão de brincar
    /// </summary>
    private void OnPlayButtonClicked()
    {
        if (creatureController != null)
        {
            creatureController.PlayWithCreature();
            AddToConversation("Ação", "Você brincou com seu Criamigo! 🎾");
            UpdateStatusText("😄 Que divertido! Adorei brincar!");
        }
    }
    
    /// <summary>
    /// Chamado quando clica no botão de descansar
    /// </summary>
    private void OnRestButtonClicked()
    {
        if (creatureController != null)
        {
            creatureController.RestCreature();
            AddToConversation("Ação", "Seu Criamigo descansou um pouco! 😴");
            UpdateStatusText("😌 Ahh, que descanso gostoso!");
        }
    }
    
    /// <summary>
    /// Chamado quando clica no botão de falar
    /// </summary>
    private void OnTalkButtonClicked()
    {
        if (sttManager == null) return;
        
        if (isRecording)
        {
            // Se está gravando, para a gravação
            sttManager.StopRecording();
        }
        else if (!isProcessing)
        {
            // Se não está processando, inicia gravação
            sttManager.StartRecording();
        }
    }
    
    /// <summary>
    /// Chamado quando clica no botão de reset (debug)
    /// </summary>
    private void OnResetButtonClicked()
    {
        if (hungerSystem != null)
        {
            hungerSystem.RestoreAll();
            AddToConversation("Sistema", "Todos os valores foram restaurados! 🔄");
            UpdateStatusText("🔄 Sistema resetado!");
        }
    }
    
    // Métodos auxiliares
    
    /// <summary>
    /// Atualiza o texto de status
    /// </summary>
    private void UpdateStatusText(string newStatus)
    {
        if (statusText != null)
        {
            statusText.text = newStatus;
        }
        
        Debug.Log($"Status: {newStatus}");
    }
    
    /// <summary>
    /// Adiciona uma nova linha à conversa
    /// </summary>
    private void AddToConversation(string speaker, string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm");
        string newLine = $"[{timestamp}] <b>{speaker}:</b> {message}\n\n";
        
        conversationHistory += newLine;
        
        if (conversationText != null)
        {
            conversationText.text = conversationHistory;
            
            // Faz scroll automático para baixo
            if (conversationScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                conversationScroll.verticalNormalizedPosition = 0f;
            }
        }
    }
    
    /// <summary>
    /// Limpa o histórico de conversa
    /// </summary>
    public void ClearConversation()
    {
        conversationHistory = "";
        if (conversationText != null)
        {
            conversationText.text = "Conversa limpa! Como posso ajudar você?";
        }
    }
    
    void OnDestroy()
    {
        // Remove listeners dos eventos para evitar vazamentos de memória
        if (hungerSystem != null)
        {
            hungerSystem.OnHungerChanged -= UpdateHungerUI;
            hungerSystem.OnEnergyChanged -= UpdateEnergyUI;
            hungerSystem.OnHappinessChanged -= UpdateHappinessUI;
            hungerSystem.OnCriticalState -= HandleCriticalState;
            hungerSystem.OnHealthyState -= HandleHealthyState;
        }
        
        // Remove outros listeners...
    }
}