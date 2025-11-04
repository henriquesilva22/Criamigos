using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Criamigos.Core;

namespace Criamigos.UI
{
    /// <summary>
    /// Sistema de gerenciamento de interface do usuário
    /// Controla todas as telas, barras de status, botões e elementos visuais
    /// Mantém interface child-friendly com feedback visual claro
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Telas Principais")]
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject gameScreen;
        [SerializeField] private GameObject settingsScreen;
        [SerializeField] private GameObject aboutScreen;
        
        [Header("Barras de Status")]
        [SerializeField] private Slider hungerBar;
        [SerializeField] private Slider happinessBar;
        [SerializeField] private Slider energyBar;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Slider evolutionBar;
        
        [Header("Textos de Status")]
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI experienceText;
        [SerializeField] private TextMeshProUGUI creatureNameText;
        [SerializeField] private TextMeshProUGUI statusText;
        
        [Header("Botões de Ação")]
        [SerializeField] private Button feedButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button sleepButton;
        [SerializeField] private Button talkButton;
        [SerializeField] private Button listenButton;
        
        [Header("Sistema de Chat")]
        [SerializeField] private GameObject speechBubble;
        [SerializeField] private TextMeshProUGUI speechText;
        [SerializeField] private Button speechCloseButton;
        [SerializeField] private ScrollRect chatHistory;
        [SerializeField] private Transform chatContent;
        [SerializeField] private GameObject chatMessagePrefab;
        
        [Header("Sistema de Microfone")]
        [SerializeField] private GameObject recordingIndicator;
        [SerializeField] private Image recordingProgressBar;
        [SerializeField] private TextMeshProUGUI recordingText;
        [SerializeField] private Button stopRecordingButton;
        
        [Header("Configurações")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle muteToggle;
        [SerializeField] private Dropdown languageDropdown;
        [SerializeField] private Button resetProgressButton;
        
        [Header("Elementos Visuais")]
        [SerializeField] private ParticleSystem happinessParticles;
        [SerializeField] private ParticleSystem evolutionParticles;
        [SerializeField] private Animator uiAnimator;
        
        [Header("Cores das Criaturas")]
        [SerializeField] private Color blueCreatureColor = Color.blue;
        [SerializeField] private Color pinkCreatureColor = Color.magenta;
        [SerializeField] private Color greenCreatureColor = Color.green;
        [SerializeField] private Color purpleCreatureColor = new Color(0.5f, 0f, 1f);
        
        // Referências dos sistemas
        private CreatureController creatureController;
        private AIResponder aiResponder;
        private TextToSpeechManager ttsManager;
        private CreatureEvolution evolutionSystem;
        private SaveSystem saveSystem;
        
        // Estado interno
        private bool isRecording = false;
        private bool isUILocked = false;
        private Coroutine speechBubbleCoroutine;
        private Queue<string> messageQueue = new Queue<string>();
        
        // Eventos
        public event Action OnFeedClicked;
        public event Action OnPlayClicked;
        public event Action OnSleepClicked;
        public event Action OnTalkClicked;
        public event Action OnListenClicked;
        
        void Awake()
        {
            // Inicializa componentes
            InitializeReferences();
            SetupEventListeners();
        }
        
        void Start()
        {
            ShowMainMenu();
            UpdateLanguageOptions();
        }
        
        void OnDestroy()
        {
            RemoveEventListeners();
        }
        
        /// <summary>
        /// Inicializa referências dos sistemas
        /// </summary>
        private void InitializeReferences()
        {
            creatureController = FindObjectOfType<CreatureController>();
            aiResponder = FindObjectOfType<AIResponder>();
            ttsManager = FindObjectOfType<TextToSpeechManager>();
            evolutionSystem = FindObjectOfType<CreatureEvolution>();
            saveSystem = FindObjectOfType<SaveSystem>();
        }
        
        /// <summary>
        /// Configura listeners dos eventos
        /// </summary>
        private void SetupEventListeners()
        {
            // Botões de ação
            feedButton?.onClick.AddListener(() => OnActionButtonClick("feed"));
            playButton?.onClick.AddListener(() => OnActionButtonClick("play"));
            sleepButton?.onClick.AddListener(() => OnActionButtonClick("sleep"));
            talkButton?.onClick.AddListener(() => OnActionButtonClick("talk"));
            listenButton?.onClick.AddListener(() => OnActionButtonClick("listen"));
            
            // Sistema de fala
            speechCloseButton?.onClick.AddListener(CloseSpeechBubble);
            stopRecordingButton?.onClick.AddListener(StopRecording);
            
            // Configurações
            volumeSlider?.onValueChanged.AddListener(OnVolumeChanged);
            muteToggle?.onValueChanged.AddListener(OnMuteChanged);
            languageDropdown?.onValueChanged.AddListener(OnLanguageChanged);
            resetProgressButton?.onClick.AddListener(OnResetProgress);
            
            // Eventos dos sistemas
            if (creatureController != null)
            {
                creatureController.OnNeedsChanged += UpdateStatusBars;
                creatureController.OnStateChanged += UpdateCreatureStatus;
                creatureController.OnActionPerformed += ShowActionFeedback;
            }
            
            if (aiResponder != null)
            {
                aiResponder.OnResponseReceived += ShowAIResponse;
                aiResponder.OnError += ShowAIError;
            }
            
            if (ttsManager != null)
            {
                ttsManager.OnSpeechStarted += OnSpeechStarted;
                ttsManager.OnSpeechCompleted += OnSpeechCompleted;
                ttsManager.OnSpeechError += OnSpeechError;
            }
            
            if (evolutionSystem != null)
            {
                evolutionSystem.OnLevelUp += OnCreatureLevelUp;
                evolutionSystem.OnEvolution += OnCreatureEvolution;
                evolutionSystem.OnColorChanged += OnCreatureColorChanged;
            }
        }
        
        /// <summary>
        /// Remove listeners dos eventos
        /// </summary>
        private void RemoveEventListeners()
        {
            // Remove listeners para evitar vazamentos de memória
            feedButton?.onClick.RemoveAllListeners();
            playButton?.onClick.RemoveAllListeners();
            sleepButton?.onClick.RemoveAllListeners();
            talkButton?.onClick.RemoveAllListeners();
            listenButton?.onClick.RemoveAllListeners();
            speechCloseButton?.onClick.RemoveAllListeners();
            stopRecordingButton?.onClick.RemoveAllListeners();
            volumeSlider?.onValueChanged.RemoveAllListeners();
            muteToggle?.onValueChanged.RemoveAllListeners();
            languageDropdown?.onValueChanged.RemoveAllListeners();
            resetProgressButton?.onClick.RemoveAllListeners();
        }
        
        /// <summary>
        /// Mostra tela do menu principal
        /// </summary>
        public void ShowMainMenu()
        {
            SetActiveScreen(mainMenuScreen);
        }
        
        /// <summary>
        /// Mostra tela do jogo
        /// </summary>
        public void ShowGameScreen()
        {
            SetActiveScreen(gameScreen);
            UpdateAllUIElements();
        }
        
        /// <summary>
        /// Mostra tela de configurações
        /// </summary>
        public void ShowSettings()
        {
            SetActiveScreen(settingsScreen);
            LoadSettingsValues();
        }
        
        /// <summary>
        /// Mostra tela sobre
        /// </summary>
        public void ShowAbout()
        {
            SetActiveScreen(aboutScreen);
        }
        
        /// <summary>
        /// Define qual tela está ativa
        /// </summary>
        private void SetActiveScreen(GameObject screen)
        {
            mainMenuScreen?.SetActive(false);
            gameScreen?.SetActive(false);
            settingsScreen?.SetActive(false);
            aboutScreen?.SetActive(false);
            
            screen?.SetActive(true);
        }
        
        /// <summary>
        /// Atualiza todos os elementos da UI
        /// </summary>
        private void UpdateAllUIElements()
        {
            if (creatureController != null)
            {
                UpdateStatusBars(creatureController.GetNeeds());
                UpdateCreatureInfo();
            }
        }
        
        /// <summary>
        /// Atualiza barras de status das necessidades
        /// </summary>
        private void UpdateStatusBars(CreatureController.CreatureNeeds needs)
        {
            if (hungerBar != null) hungerBar.value = needs.hunger / 100f;
            if (happinessBar != null) happinessBar.value = needs.happiness / 100f;
            if (energyBar != null) energyBar.value = needs.energy / 100f;
            if (healthBar != null) healthBar.value = needs.health / 100f;
            
            // Atualiza cores das barras baseado nos valores
            UpdateBarColors(needs);
            
            // Mostra efeitos visuais se necessário
            if (needs.happiness > 80f && happinessParticles != null && !happinessParticles.isPlaying)
            {
                happinessParticles.Play();
            }
        }
        
        /// <summary>
        /// Atualiza cores das barras baseado nos valores
        /// </summary>
        private void UpdateBarColors(CreatureController.CreatureNeeds needs)
        {
            // Fome: Verde -> Amarelo -> Vermelho
            UpdateBarColor(hungerBar, needs.hunger, Color.red, Color.yellow, Color.green);
            
            // Felicidade: Vermelho -> Amarelo -> Verde
            UpdateBarColor(happinessBar, needs.happiness, Color.red, Color.yellow, Color.green);
            
            // Energia: Vermelho -> Amarelo -> Azul
            UpdateBarColor(energyBar, needs.energy, Color.red, Color.yellow, Color.cyan);
            
            // Saúde: Vermelho -> Amarelo -> Verde
            UpdateBarColor(healthBar, needs.health, Color.red, Color.yellow, Color.green);
        }
        
        /// <summary>
        /// Atualiza cor de uma barra específica
        /// </summary>
        private void UpdateBarColor(Slider bar, float value, Color lowColor, Color midColor, Color highColor)
        {
            if (bar?.fillRect?.GetComponent<Image>() == null) return;
            
            Image fillImage = bar.fillRect.GetComponent<Image>();
            
            if (value < 33f)
            {
                fillImage.color = Color.Lerp(lowColor, midColor, value / 33f);
            }
            else
            {
                fillImage.color = Color.Lerp(midColor, highColor, (value - 33f) / 67f);
            }
        }
        
        /// <summary>
        /// Atualiza informações da criatura
        /// </summary>
        private void UpdateCreatureInfo()
        {
            if (evolutionSystem != null)
            {
                if (levelText != null)
                    levelText.text = $"Nível {evolutionSystem.GetCurrentLevel()}";
                    
                if (experienceText != null)
                    experienceText.text = $"XP: {evolutionSystem.GetCurrentExperience()}/{evolutionSystem.GetExperienceForNextLevel()}";
                    
                if (evolutionBar != null)
                {
                    float progressPercent = evolutionSystem.GetEvolutionProgress();
                    evolutionBar.value = progressPercent / 100f;
                }
            }
            
            if (creatureController != null && creatureNameText != null)
            {
                creatureNameText.text = creatureController.GetCreatureName();
            }
        }
        
        /// <summary>
        /// Atualiza texto de status da criatura
        /// </summary>
        private void UpdateCreatureStatus(CreatureController.CreatureState state, string message)
        {
            if (statusText != null)
            {
                statusText.text = GetStatusMessage(state);
                
                // Anima o texto de status
                if (uiAnimator != null)
                {
                    uiAnimator.SetTrigger("StatusUpdate");
                }
            }
        }
        
        /// <summary>
        /// Obtém mensagem amigável para o estado
        /// </summary>
        private string GetStatusMessage(CreatureController.CreatureState state)
        {
            switch (state)
            {
                case CreatureController.CreatureState.Happy: return "😊 Muito feliz!";
                case CreatureController.CreatureState.Hungry: return "😋 Com fome";
                case CreatureController.CreatureState.Tired: return "😴 Sonolento";
                case CreatureController.CreatureState.Sick: return "🤒 Doentinho";
                case CreatureController.CreatureState.Playing: return "🎮 Brincando";
                case CreatureController.CreatureState.Sleeping: return "💤 Dormindo";
                case CreatureController.CreatureState.Talking: return "💬 Conversando";
                default: return "😐 Normal";
            }
        }
        
        /// <summary>
        /// Manipula cliques nos botões de ação
        /// </summary>
        private void OnActionButtonClick(string action)
        {
            if (isUILocked) return;
            
            switch (action)
            {
                case "feed":
                    OnFeedClicked?.Invoke();
                    ShowActionFeedback("Alimentando...", Color.green);
                    break;
                    
                case "play":
                    OnPlayClicked?.Invoke();
                    ShowActionFeedback("Brincando...", Color.yellow);
                    break;
                    
                case "sleep":
                    OnSleepClicked?.Invoke();
                    ShowActionFeedback("Dormindo...", Color.blue);
                    break;
                    
                case "talk":
                    OnTalkClicked?.Invoke();
                    ShowSpeechBubble("Olá! Como você está?");
                    break;
                    
                case "listen":
                    StartRecording();
                    break;
            }
            
            // Efeito visual no botão
            StartCoroutine(AnimateButton(GetButtonForAction(action)));
        }
        
        /// <summary>
        /// Obtém botão para uma ação específica
        /// </summary>
        private Button GetButtonForAction(string action)
        {
            switch (action)
            {
                case "feed": return feedButton;
                case "play": return playButton;
                case "sleep": return sleepButton;
                case "talk": return talkButton;
                case "listen": return listenButton;
                default: return null;
            }
        }
        
        /// <summary>
        /// Anima botão quando clicado
        /// </summary>
        private IEnumerator AnimateButton(Button button)
        {
            if (button == null) yield break;
            
            Transform buttonTransform = button.transform;
            Vector3 originalScale = buttonTransform.localScale;
            
            // Animação de "squeeze"
            buttonTransform.localScale = originalScale * 0.9f;
            yield return new WaitForSeconds(0.1f);
            buttonTransform.localScale = originalScale * 1.1f;
            yield return new WaitForSeconds(0.1f);
            buttonTransform.localScale = originalScale;
        }
        
        /// <summary>
        /// Mostra feedback visual de ação
        /// </summary>
        private void ShowActionFeedback(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
                
                // Volta para cor normal após 2 segundos
                StartCoroutine(RestoreStatusTextColor());
            }
        }
        
        /// <summary>
        /// Restaura cor normal do texto de status
        /// </summary>
        private IEnumerator RestoreStatusTextColor()
        {
            yield return new WaitForSeconds(2f);
            
            if (statusText != null)
            {
                statusText.color = Color.white;
            }
        }
        
        /// <summary>
        /// Mostra balão de fala
        /// </summary>
        public void ShowSpeechBubble(string message)
        {
            if (speechBubble == null || speechText == null) return;
            
            speechText.text = message;
            speechBubble.SetActive(true);
            
            // Auto-fecha após 5 segundos
            if (speechBubbleCoroutine != null)
                StopCoroutine(speechBubbleCoroutine);
                
            speechBubbleCoroutine = StartCoroutine(AutoCloseSpeechBubble());
            
            // Adiciona à história do chat
            AddToChatHistory("Criatura", message);
        }
        
        /// <summary>
        /// Fecha balão de fala automaticamente
        /// </summary>
        private IEnumerator AutoCloseSpeechBubble()
        {
            yield return new WaitForSeconds(5f);
            CloseSpeechBubble();
        }
        
        /// <summary>
        /// Fecha balão de fala
        /// </summary>
        public void CloseSpeechBubble()
        {
            if (speechBubble != null)
                speechBubble.SetActive(false);
                
            if (speechBubbleCoroutine != null)
            {
                StopCoroutine(speechBubbleCoroutine);
                speechBubbleCoroutine = null;
            }
        }
        
        /// <summary>
        /// Adiciona mensagem à história do chat
        /// </summary>
        private void AddToChatHistory(string sender, string message)
        {
            if (chatContent == null || chatMessagePrefab == null) return;
            
            GameObject messageObj = Instantiate(chatMessagePrefab, chatContent);
            TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
            
            if (messageText != null)
            {
                messageText.text = $"{sender}: {message}";
                
                // Define cor baseada no remetente
                if (sender == "Você")
                    messageText.color = Color.cyan;
                else
                    messageText.color = Color.white;
            }
            
            // Scroll para baixo
            StartCoroutine(ScrollToBottom());
        }
        
        /// <summary>
        /// Scroll para o final do chat
        /// </summary>
        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            
            if (chatHistory != null)
                chatHistory.verticalNormalizedPosition = 0f;
        }
        
        /// <summary>
        /// Inicia gravação de áudio
        /// </summary>
        private void StartRecording()
        {
            if (isRecording) return;
            
            isRecording = true;
            recordingIndicator?.SetActive(true);
            
            if (recordingText != null)
                recordingText.text = "🎤 Fale agora...";
            
            // Inicia animação do indicador
            StartCoroutine(AnimateRecordingIndicator());
            
            // Chama sistema de STT (implementar quando disponível)
            OnListenClicked?.Invoke();
        }
        
        /// <summary>
        /// Para gravação de áudio
        /// </summary>
        private void StopRecording()
        {
            isRecording = false;
            recordingIndicator?.SetActive(false);
        }
        
        /// <summary>
        /// Anima indicador de gravação
        /// </summary>
        private IEnumerator AnimateRecordingIndicator()
        {
            float time = 0f;
            
            while (isRecording && recordingProgressBar != null)
            {
                time += Time.deltaTime;
                recordingProgressBar.fillAmount = (Mathf.Sin(time * 3f) + 1f) / 2f;
                yield return null;
            }
        }
        
        /// <summary>
        /// Resposta recebida da IA
        /// </summary>
        private void ShowAIResponse(string response)
        {
            ShowSpeechBubble(response);
            
            // Reproduz áudio se TTS estiver disponível
            if (ttsManager != null)
            {
                ttsManager.SpeakText(response);
            }
        }
        
        /// <summary>
        /// Erro da IA
        /// </summary>
        private void ShowAIError(string error)
        {
            ShowActionFeedback("Ops! Não consegui entender", Color.red);
        }
        
        /// <summary>
        /// Eventos do sistema de fala
        /// </summary>
        private void OnSpeechStarted()
        {
            LockUI(true);
        }
        
        private void OnSpeechCompleted()
        {
            LockUI(false);
        }
        
        private void OnSpeechError(string error)
        {
            LockUI(false);
            ShowActionFeedback("Erro na fala", Color.red);
        }
        
        /// <summary>
        /// Eventos do sistema de evolução
        /// </summary>
        private void OnCreatureLevelUp(int newLevel)
        {
            ShowActionFeedback($"🎉 Nível {newLevel}!", Color.gold);
            
            if (evolutionParticles != null)
                evolutionParticles.Play();
                
            UpdateCreatureInfo();
        }
        
        private void OnCreatureEvolution(int stage)
        {
            ShowActionFeedback($"✨ Evolução {stage}!", Color.magenta);
            
            if (evolutionParticles != null)
                evolutionParticles.Play();
        }
        
        private void OnCreatureColorChanged(CreatureEvolution.CreatureColor color)
        {
            string colorName = GetColorName(color);
            ShowActionFeedback($"🌈 Agora sou {colorName}!", GetUIColor(color));
        }
        
        /// <summary>
        /// Obtém nome amigável da cor
        /// </summary>
        private string GetColorName(CreatureEvolution.CreatureColor color)
        {
            switch (color)
            {
                case CreatureEvolution.CreatureColor.Blue: return "azul";
                case CreatureEvolution.CreatureColor.Pink: return "rosa";
                case CreatureEvolution.CreatureColor.Green: return "verde";
                case CreatureEvolution.CreatureColor.Purple: return "roxo";
                default: return "colorido";
            }
        }
        
        /// <summary>
        /// Obtém cor para UI baseada na cor da criatura
        /// </summary>
        private Color GetUIColor(CreatureEvolution.CreatureColor color)
        {
            switch (color)
            {
                case CreatureEvolution.CreatureColor.Blue: return blueCreatureColor;
                case CreatureEvolution.CreatureColor.Pink: return pinkCreatureColor;
                case CreatureEvolution.CreatureColor.Green: return greenCreatureColor;
                case CreatureEvolution.CreatureColor.Purple: return purpleCreatureColor;
                default: return Color.white;
            }
        }
        
        /// <summary>
        /// Trava/destrava interface
        /// </summary>
        private void LockUI(bool locked)
        {
            isUILocked = locked;
            
            // Desabilita botões durante operações
            feedButton.interactable = !locked;
            playButton.interactable = !locked;
            sleepButton.interactable = !locked;
            talkButton.interactable = !locked;
            listenButton.interactable = !locked;
        }
        
        /// <summary>
        /// Eventos das configurações
        /// </summary>
        private void OnVolumeChanged(float volume)
        {
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat("MasterVolume", volume);
        }
        
        private void OnMuteChanged(bool muted)
        {
            AudioListener.pause = muted;
            PlayerPrefs.SetInt("AudioMuted", muted ? 1 : 0);
        }
        
        private void OnLanguageChanged(int index)
        {
            string[] languages = { "pt-BR", "en-US", "es-ES" };
            if (index < languages.Length)
            {
                PlayerPrefs.SetString("Language", languages[index]);
                // Implementar mudança de idioma
            }
        }
        
        private void OnResetProgress()
        {
            // Confirmação antes de resetar
            // Implementar modal de confirmação
            if (saveSystem != null)
            {
                saveSystem.ResetAllProgress();
                ShowActionFeedback("Progresso resetado", Color.orange);
            }
        }
        
        /// <summary>
        /// Carrega valores das configurações
        /// </summary>
        private void LoadSettingsValues()
        {
            if (volumeSlider != null)
                volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
                
            if (muteToggle != null)
                muteToggle.isOn = PlayerPrefs.GetInt("AudioMuted", 0) == 1;
                
            if (languageDropdown != null)
            {
                string currentLang = PlayerPrefs.GetString("Language", "pt-BR");
                string[] languages = { "pt-BR", "en-US", "es-ES" };
                
                for (int i = 0; i < languages.Length; i++)
                {
                    if (languages[i] == currentLang)
                    {
                        languageDropdown.value = i;
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Atualiza opções de idioma
        /// </summary>
        private void UpdateLanguageOptions()
        {
            if (languageDropdown == null) return;
            
            languageDropdown.options.Clear();
            languageDropdown.options.Add(new TMP_Dropdown.OptionData("Português"));
            languageDropdown.options.Add(new TMP_Dropdown.OptionData("English"));
            languageDropdown.options.Add(new TMP_Dropdown.OptionData("Español"));
        }
        
        /// <summary>
        /// Métodos públicos para controle externo
        /// </summary>
        public void SetCreatureColor(CreatureEvolution.CreatureColor color)
        {
            // Atualiza temas da UI baseado na cor da criatura
            Color uiColor = GetUIColor(color);
            
            // Aplica cor aos elementos relevantes
            if (evolutionBar?.fillRect?.GetComponent<Image>() != null)
                evolutionBar.fillRect.GetComponent<Image>().color = uiColor;
        }
        
        public void ShowMessage(string message)
        {
            ShowSpeechBubble(message);
        }
        
        public void ProcessUserInput(string input)
        {
            AddToChatHistory("Você", input);
            
            // Envia para IA se disponível
            if (aiResponder != null)
            {
                aiResponder.ProcessUserInput(input);
            }
        }
    }
}