using System;
using System.Collections;
using UnityEngine;
using Criamigos.AI;

namespace Criamigos.Core
{
    /// <summary>
    /// Controlador principal da criatura que gerencia estados, necessidades e comportamentos
    /// Integra sistemas de IA, animações e evolução para criar experiência interativa completa
    /// Monitora necessidades da criatura e solicita ajuda automaticamente quando necessário
    /// </summary>
    public class CreatureController : MonoBehaviour
    {
        [Header("Componentes da Criatura")]
        [SerializeField] private Animator creatureAnimator; // Animator para animações da criatura
        [SerializeField] private AudioSource creatureAudioSource; // AudioSource para sons da criatura
        [SerializeField] private Transform lookAtTarget; // Target para a criatura olhar (câmera/jogador)
        
        [Header("Sistemas Conectados")]
        [SerializeField] private AIResponder aiResponder; // Sistema de IA para respostas
        [SerializeField] private TextToSpeechManager ttsManager; // Sistema de TTS
        [SerializeField] private CreatureEvolution evolutionSystem; // Sistema de evolução
        
        [Header("Estados da Criatura")]
        [SerializeField] private float maxHunger = 100f; // Valor máximo de fome
        [SerializeField] private float maxEnergy = 100f; // Valor máximo de energia
        [SerializeField] private float maxHappiness = 100f; // Valor máximo de felicidade
        [SerializeField] private float currentHunger = 80f; // Fome atual
        [SerializeField] private float currentEnergy = 80f; // Energia atual
        [SerializeField] private float currentHappiness = 80f; // Felicidade atual
        
        [Header("Taxas de Diminuição (por minuto)")]
        [SerializeField] private float hungerDecayRate = 8f; // Quanto a fome diminui por minuto
        [SerializeField] private float energyDecayRate = 6f; // Quanto a energia diminui por minuto
        [SerializeField] private float happinessDecayRate = 4f; // Quanto a felicidade diminui por minuto
        
        [Header("Limites Críticos")]
        [SerializeField] private float criticalLevel = 20f; // Abaixo deste nível é crítico
        [SerializeField] private float warningLevel = 40f; // Abaixo deste nível mostra aviso
        [SerializeField] private float minRequestInterval = 30f; // Intervalo mínimo entre pedidos de ajuda
        
        [Header("Configurações de Comportamento")]
        [SerializeField] private float interactionCooldown = 2f; // Tempo entre interações
        [SerializeField] private bool enableAutomaticRequests = true; // Ativa pedidos automáticos de ajuda
        [SerializeField] private bool enableIdleAnimations = true; // Ativa animações quando parado
        [SerializeField] private float idleAnimationInterval = 10f; // Intervalo entre animações idle
        
        [Header("Parâmetros do Animator")]
        [SerializeField] private string hungerParameter = "Hunger"; // Parâmetro de fome no Animator
        [SerializeField] private string energyParameter = "Energy"; // Parâmetro de energia no Animator
        [SerializeField] private string happinessParameter = "Happiness"; // Parâmetro de felicidade no Animator
        [SerializeField] private string emotionParameter = "Emotion"; // Parâmetro de emoção (0=sad, 1=neutral, 2=happy)
        [SerializeField] private string actionTrigger = "Action"; // Trigger para ações (comer, brincar, dormir)
        [SerializeField] private string actionTypeParameter = "ActionType"; // Tipo da ação (0=eat, 1=play, 2=sleep)
        
        [Header("Sons da Criatura")]
        [SerializeField] private AudioClip[] happySounds; // Sons quando feliz
        [SerializeField] private AudioClip[] sadSounds; // Sons quando triste
        [SerializeField] private AudioClip[] hungrySounds; // Sons quando com fome
        [SerializeField] private AudioClip[] tiredSounds; // Sons quando cansado
        [SerializeField] private AudioClip[] actionSounds; // Sons durante ações
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true; // Ativa logs detalhados
        [SerializeField] private bool showStatusInGUI = false; // Mostra status na tela (debug)
        
        // Eventos públicos para outros sistemas
        public event Action<float, float> OnHungerChanged; // (valor, normalizado)
        public event Action<float, float> OnEnergyChanged; // (valor, normalizado)
        public event Action<float, float> OnHappinessChanged; // (valor, normalizado)
        public event Action<CreatureState> OnStateChanged; // Quando muda estado geral
        public event Action<CreatureAction> OnActionPerformed; // Quando realiza ação
        public event Action OnEvolutionReady; // Quando está pronto para evoluir
        
        // Enums para estados e ações
        public enum CreatureState { Critical, Warning, Normal, Happy, Sleeping }
        public enum CreatureAction { Eat, Play, Sleep, Idle, Talk }
        public enum EmotionLevel { Sad = 0, Neutral = 1, Happy = 2 }
        
        // Estado interno da criatura
        private CreatureState currentState = CreatureState.Normal;
        private bool isPerformingAction = false;
        private bool isAsleep = false;
        private float lastRequestTime = 0f;
        private float lastInteractionTime = 0f;
        private float lastIdleAnimationTime = 0f;
        private System.Random randomGenerator;
        
        // Estatísticas de cuidado para evolução
        private int totalFeedings = 0;
        private int totalPlaySessions = 0;
        private int totalRestSessions = 0;
        private int totalConversations = 0;
        private float totalCareTime = 0f;
        
        void Awake()
        {
            randomGenerator = new System.Random();
            
            // Configura componentes se não atribuídos
            if (creatureAnimator == null)
                creatureAnimator = GetComponent<Animator>();
                
            if (creatureAudioSource == null)
                creatureAudioSource = GetComponent<AudioSource>();
        }
        
        void Start()
        {
            // Inicializa valores de necessidades
            currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
            currentHappiness = Mathf.Clamp(currentHappiness, 0f, maxHappiness);
            
            // Conecta com sistemas de IA se disponíveis
            SetupAIConnections();
            
            // Atualiza estado inicial
            UpdateCreatureState();
            UpdateAnimatorParameters();
            
            // Inicia sistemas de tempo
            StartCoroutine(NeedsDecayLoop());
            StartCoroutine(IdleAnimationLoop());
            
            if (enableDebugLogs)
                Debug.Log("[CreatureController] Criatura inicializada - Estado: " + currentState);
        }
        
        /// <summary>
        /// Conecta com sistemas de IA disponíveis na cena
        /// </summary>
        private void SetupAIConnections()
        {
            // Busca sistemas se não atribuídos
            if (aiResponder == null)
                aiResponder = FindObjectOfType<AIResponder>();
                
            if (ttsManager == null)
                ttsManager = FindObjectOfType<TextToSpeechManager>();
                
            if (evolutionSystem == null)
                evolutionSystem = GetComponent<CreatureEvolution>();
            
            // Conecta eventos de IA
            if (aiResponder != null)
            {
                aiResponder.OnResponseReceived += HandleAIResponse;
            }
            
            if (ttsManager != null)
            {
                ttsManager.OnSpeechStarted += HandleSpeechStarted;
                ttsManager.OnSpeechCompleted += HandleSpeechCompleted;
            }
        }
        
        /// <summary>
        /// Loop principal de diminuição das necessidades
        /// </summary>
        private IEnumerator NeedsDecayLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(60f); // A cada minuto
                
                if (!isAsleep) // Necessidades não diminuem durante o sono
                {
                    DecayNeeds();
                }
                
                totalCareTime += 1f / 60f; // Adiciona tempo de cuidado (em horas)
            }
        }
        
        /// <summary>
        /// Diminui as necessidades com o passar do tempo
        /// </summary>
        private void DecayNeeds()
        {
            // Aplica decaimento baseado nas taxas configuradas
            currentHunger = Mathf.Max(0f, currentHunger - hungerDecayRate);
            currentEnergy = Mathf.Max(0f, currentEnergy - energyDecayRate);
            currentHappiness = Mathf.Max(0f, currentHappiness - happinessDecayRate);
            
            // Notifica mudanças
            NotifyNeedsChanged();
            
            // Atualiza estado e verifica se precisa pedir ajuda
            UpdateCreatureState();
            CheckAutomaticRequests();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[CreatureController] Necessidades atualizadas - H:{currentHunger:F1} E:{currentEnergy:F1} F:{currentHappiness:F1}");
            }
        }
        
        /// <summary>
        /// Notifica outros sistemas sobre mudanças nas necessidades
        /// </summary>
        private void NotifyNeedsChanged()
        {
            OnHungerChanged?.Invoke(currentHunger, currentHunger / maxHunger);
            OnEnergyChanged?.Invoke(currentEnergy, currentEnergy / maxEnergy);
            OnHappinessChanged?.Invoke(currentHappiness, currentHappiness / maxHappiness);
        }
        
        /// <summary>
        /// Atualiza o estado geral da criatura baseado nas necessidades
        /// </summary>
        private void UpdateCreatureState()
        {
            CreatureState newState;
            
            if (isAsleep)
            {
                newState = CreatureState.Sleeping;
            }
            else if (currentHunger <= criticalLevel || currentEnergy <= criticalLevel || currentHappiness <= criticalLevel)
            {
                newState = CreatureState.Critical;
            }
            else if (currentHunger <= warningLevel || currentEnergy <= warningLevel || currentHappiness <= warningLevel)
            {
                newState = CreatureState.Warning;
            }
            else if (currentHunger >= 80f && currentEnergy >= 80f && currentHappiness >= 80f)
            {
                newState = CreatureState.Happy;
            }
            else
            {
                newState = CreatureState.Normal;
            }
            
            // Se mudou de estado, notifica
            if (newState != currentState)
            {
                CreatureState previousState = currentState;
                currentState = newState;
                
                OnStateChanged?.Invoke(currentState);
                HandleStateChange(previousState, newState);
                
                if (enableDebugLogs)
                    Debug.Log($"[CreatureController] Estado mudou: {previousState} → {newState}");
            }
            
            // Atualiza parâmetros do Animator
            UpdateAnimatorParameters();
        }
        
        /// <summary>
        /// Manipula mudanças de estado com reações apropriadas
        /// </summary>
        private void HandleStateChange(CreatureState from, CreatureState to)
        {
            switch (to)
            {
                case CreatureState.Critical:
                    PlayStateSound(sadSounds);
                    SetEmotionAnimation(EmotionLevel.Sad);
                    break;
                    
                case CreatureState.Happy:
                    PlayStateSound(happySounds);
                    SetEmotionAnimation(EmotionLevel.Happy);
                    break;
                    
                default:
                    SetEmotionAnimation(EmotionLevel.Neutral);
                    break;
            }
        }
        
        /// <summary>
        /// Atualiza parâmetros do Animator baseado no estado atual
        /// </summary>
        private void UpdateAnimatorParameters()
        {
            if (creatureAnimator == null) return;
            
            // Atualiza valores normalizados (0-1)
            creatureAnimator.SetFloat(hungerParameter, currentHunger / maxHunger);
            creatureAnimator.SetFloat(energyParameter, currentEnergy / maxEnergy);
            creatureAnimator.SetFloat(happinessParameter, currentHappiness / maxHappiness);
            
            // Determina emoção baseada no estado geral
            EmotionLevel emotion = DetermineEmotionLevel();
            creatureAnimator.SetInteger(emotionParameter, (int)emotion);
        }
        
        /// <summary>
        /// Determina nível de emoção baseado nas necessidades atuais
        /// </summary>
        private EmotionLevel DetermineEmotionLevel()
        {
            float averageNeeds = (currentHunger + currentEnergy + currentHappiness) / 3f;
            float normalizedAverage = averageNeeds / ((maxHunger + maxEnergy + maxHappiness) / 3f);
            
            if (normalizedAverage >= 0.7f)
                return EmotionLevel.Happy;
            else if (normalizedAverage <= 0.3f)
                return EmotionLevel.Sad;
            else
                return EmotionLevel.Neutral;
        }
        
        /// <summary>
        /// Define animação de emoção no Animator
        /// </summary>
        private void SetEmotionAnimation(EmotionLevel emotion)
        {
            if (creatureAnimator != null)
            {
                creatureAnimator.SetInteger(emotionParameter, (int)emotion);
            }
        }
        
        /// <summary>
        /// Verifica se deve fazer pedidos automáticos de ajuda
        /// </summary>
        private void CheckAutomaticRequests()
        {
            if (!enableAutomaticRequests || aiResponder == null) return;
            
            float timeSinceLastRequest = Time.time - lastRequestTime;
            if (timeSinceLastRequest < minRequestInterval) return;
            
            // Verifica se alguma necessidade está crítica
            string requestMessage = GetAutomaticRequestMessage();
            if (!string.IsNullOrEmpty(requestMessage))
            {
                RequestAIResponse(requestMessage);
                lastRequestTime = Time.time;
            }
        }
        
        /// <summary>
        /// Gera mensagem de pedido automático baseado nas necessidades
        /// </summary>
        private string GetAutomaticRequestMessage()
        {
            if (currentHunger <= criticalLevel)
            {
                return "Estou com muita fome! Pode me dar comida, por favor?";
            }
            else if (currentEnergy <= criticalLevel)
            {
                return "Estou muito cansado... preciso descansar um pouquinho!";
            }
            else if (currentHappiness <= criticalLevel)
            {
                return "Estou meio tristinho... que tal brincarmos juntos?";
            }
            
            return ""; // Sem pedidos necessários
        }
        
        /// <summary>
        /// Loop de animações idle quando a criatura está parada
        /// </summary>
        private IEnumerator IdleAnimationLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(idleAnimationInterval);
                
                if (enableIdleAnimations && !isPerformingAction && !isAsleep)
                {
                    PlayIdleAnimation();
                }
            }
        }
        
        /// <summary>
        /// Reproduz animação idle aleatória
        /// </summary>
        private void PlayIdleAnimation()
        {
            if (creatureAnimator == null) return;
            
            // Trigger animação idle baseada no estado atual
            switch (currentState)
            {
                case CreatureState.Happy:
                    creatureAnimator.SetTrigger("IdleHappy");
                    break;
                case CreatureState.Critical:
                    creatureAnimator.SetTrigger("IdleSad");
                    break;
                default:
                    creatureAnimator.SetTrigger("IdleNeutral");
                    break;
            }
            
            lastIdleAnimationTime = Time.time;
        }
        
        // Métodos públicos de ação - chamados pela UI ou outros sistemas
        
        /// <summary>
        /// Alimenta a criatura
        /// </summary>
        /// <param name="amount">Quantidade de fome a restaurar (padrão: 30)</param>
        public void Feed(float amount = 30f)
        {
            if (!CanPerformAction()) return;
            
            StartCoroutine(PerformActionCoroutine(CreatureAction.Eat, amount));
            totalFeedings++;
        }
        
        /// <summary>
        /// Brinca com a criatura
        /// </summary>
        /// <param name="amount">Quantidade de felicidade a restaurar (padrão: 25)</param>
        public void Play(float amount = 25f)
        {
            if (!CanPerformAction()) return;
            
            StartCoroutine(PerformActionCoroutine(CreatureAction.Play, amount));
            totalPlaySessions++;
        }
        
        /// <summary>
        /// Faz a criatura descansar
        /// </summary>
        /// <param name="amount">Quantidade de energia a restaurar (padrão: 35)</param>
        public void Rest(float amount = 35f)
        {
            if (!CanPerformAction()) return;
            
            StartCoroutine(PerformActionCoroutine(CreatureAction.Sleep, amount));
            totalRestSessions++;
        }
        
        /// <summary>
        /// Inicia conversa com a criatura
        /// </summary>
        /// <param name="message">Mensagem do jogador</param>
        public void StartConversation(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            
            totalConversations++;
            
            // Constrói contexto para a IA
            string context = BuildConversationContext();
            RequestAIResponse(message, context);
            
            // Animação de escutar
            if (creatureAnimator != null)
            {
                creatureAnimator.SetTrigger("Listen");
            }
        }
        
        /// <summary>
        /// Verifica se pode realizar uma ação
        /// </summary>
        private bool CanPerformAction()
        {
            float timeSinceLastInteraction = Time.time - lastInteractionTime;
            return !isPerformingAction && timeSinceLastInteraction >= interactionCooldown;
        }
        
        /// <summary>
        /// Coroutine para realizar ações com animações
        /// </summary>
        private IEnumerator PerformActionCoroutine(CreatureAction action, float amount)
        {
            isPerformingAction = true;
            lastInteractionTime = Time.time;
            
            // Configura animação
            if (creatureAnimator != null)
            {
                creatureAnimator.SetInteger(actionTypeParameter, (int)action);
                creatureAnimator.SetTrigger(actionTrigger);
            }
            
            // Reproduz som da ação
            PlayActionSound(action);
            
            // Espera um pouco para a animação começar
            yield return new WaitForSeconds(0.5f);
            
            // Aplica efeito da ação
            ApplyActionEffect(action, amount);
            
            // Aguarda conclusão da animação
            yield return new WaitForSeconds(1.5f);
            
            // Reação pós-ação
            ReactToAction(action);
            
            isPerformingAction = false;
            
            OnActionPerformed?.Invoke(action);
            
            // Verifica se está pronto para evoluir
            CheckEvolutionReadiness();
        }
        
        /// <summary>
        /// Aplica efeito da ação nas necessidades
        /// </summary>
        private void ApplyActionEffect(CreatureAction action, float amount)
        {
            switch (action)
            {
                case CreatureAction.Eat:
                    currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
                    // Comer também aumenta um pouco a felicidade
                    currentHappiness = Mathf.Min(maxHappiness, currentHappiness + amount * 0.3f);
                    break;
                    
                case CreatureAction.Play:
                    currentHappiness = Mathf.Min(maxHappiness, currentHappiness + amount);
                    // Brincar consome um pouco de energia
                    currentEnergy = Mathf.Max(0f, currentEnergy - amount * 0.2f);
                    break;
                    
                case CreatureAction.Sleep:
                    currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
                    // Dormir também restaura um pouco a felicidade
                    currentHappiness = Mathf.Min(maxHappiness, currentHappiness + amount * 0.2f);
                    break;
            }
            
            // Notifica mudanças e atualiza estado
            NotifyNeedsChanged();
            UpdateCreatureState();
        }
        
        /// <summary>
        /// Reação da criatura após realizar ação
        /// </summary>
        private void ReactToAction(CreatureAction action)
        {
            if (aiResponder == null) return;
            
            string[] reactions = GetActionReactions(action);
            if (reactions.Length > 0)
            {
                string reaction = reactions[randomGenerator.Next(reactions.Length)];
                RequestAIResponse(reaction);
            }
        }
        
        /// <summary>
        /// Obtém reações possíveis para cada tipo de ação
        /// </summary>
        private string[] GetActionReactions(CreatureAction action)
        {
            switch (action)
            {
                case CreatureAction.Eat:
                    return new string[] {
                        "Que delícia! Muito obrigado!",
                        "Estava com fome! Você é o melhor!",
                        "Hmm, que gostoso! Adorei!"
                    };
                    
                case CreatureAction.Play:
                    return new string[] {
                        "Que divertido! Vamos brincar mais!",
                        "Adorei! Você é muito legal!",
                        "Eba! Brincar é a melhor coisa!"
                    };
                    
                case CreatureAction.Sleep:
                    return new string[] {
                        "Ahh, que descanso gostoso!",
                        "Estava precisando! Me sinto melhor!",
                        "Obrigado por me deixar descansar!"
                    };
                    
                default:
                    return new string[0];
            }
        }
        
        /// <summary>
        /// Constrói contexto para conversas com IA
        /// </summary>
        private string BuildConversationContext()
        {
            string needsContext = "";
            
            if (currentHunger <= warningLevel)
                needsContext += "com fome ";
            if (currentEnergy <= warningLevel)
                needsContext += "cansado ";
            if (currentHappiness <= warningLevel)
                needsContext += "triste ";
                
            if (string.IsNullOrEmpty(needsContext))
                needsContext = "bem cuidado ";
            
            return $"A criatura está {needsContext}e no estado {currentState}";
        }
        
        /// <summary>
        /// Solicita resposta da IA com contexto
        /// </summary>
        private void RequestAIResponse(string message, string context = "")
        {
            if (aiResponder != null)
            {
                if (string.IsNullOrEmpty(context))
                    context = BuildConversationContext();
                    
                aiResponder.RequestResponse(message, context);
            }
        }
        
        /// <summary>
        /// Verifica se está pronto para evoluir
        /// </summary>
        private void CheckEvolutionReadiness()
        {
            if (evolutionSystem == null) return;
            
            // Critérios básicos para evolução
            bool hasEnoughCare = totalFeedings >= 10 && totalPlaySessions >= 8 && totalRestSessions >= 5;
            bool hasGoodStats = currentHunger >= 70f && currentEnergy >= 70f && currentHappiness >= 70f;
            bool hasEnoughTime = totalCareTime >= 2f; // 2 horas de cuidado
            
            if (hasEnoughCare && hasGoodStats && hasEnoughTime && !evolutionSystem.IsEvolutionReady())
            {
                evolutionSystem.SetReadyForEvolution(true);
                OnEvolutionReady?.Invoke();
                
                RequestAIResponse("Estou me sentindo diferente... acho que estou crescendo!");
            }
        }
        
        // Handlers para eventos de IA
        
        /// <summary>
        /// Manipula resposta recebida da IA
        /// </summary>
        private void HandleAIResponse(string response)
        {
            if (ttsManager != null)
            {
                // Determina emoção para o TTS
                string emotion = DetermineEmotionForTTS();
                ttsManager.Speak(response, emotion);
            }
        }
        
        /// <summary>
        /// Determina emoção para o sistema TTS
        /// </summary>
        private string DetermineEmotionForTTS()
        {
            switch (currentState)
            {
                case CreatureState.Happy: return "happy";
                case CreatureState.Critical: return "sad";
                case CreatureState.Warning: 
                    if (currentHunger <= warningLevel) return "hungry";
                    if (currentEnergy <= warningLevel) return "tired";
                    return "sad";
                default: return "";
            }
        }
        
        /// <summary>
        /// Manipula início de fala
        /// </summary>
        private void HandleSpeechStarted(string text)
        {
            if (creatureAnimator != null)
            {
                creatureAnimator.SetTrigger("Talk");
            }
        }
        
        /// <summary>
        /// Manipula fim de fala
        /// </summary>
        private void HandleSpeechCompleted()
        {
            // A criatura pode fazer pequenos gestos após falar
            if (creatureAnimator != null && currentState == CreatureState.Happy)
            {
                creatureAnimator.SetTrigger("IdleHappy");
            }
        }
        
        // Métodos utilitários para sons
        
        private void PlayStateSound(AudioClip[] soundArray)
        {
            if (soundArray != null && soundArray.Length > 0 && creatureAudioSource != null)
            {
                AudioClip randomSound = soundArray[randomGenerator.Next(soundArray.Length)];
                creatureAudioSource.PlayOneShot(randomSound);
            }
        }
        
        private void PlayActionSound(CreatureAction action)
        {
            if (actionSounds != null && actionSounds.Length > 0 && creatureAudioSource != null)
            {
                AudioClip actionSound = actionSounds[randomGenerator.Next(actionSounds.Length)];
                creatureAudioSource.PlayOneShot(actionSound);
            }
        }
        
        // Métodos públicos para obter informações
        
        public float GetHungerNormalized() => currentHunger / maxHunger;
        public float GetEnergyNormalized() => currentEnergy / maxEnergy;
        public float GetHappinessNormalized() => currentHappiness / maxHappiness;
        
        public float GetHunger() => currentHunger;
        public float GetEnergy() => currentEnergy;
        public float GetHappiness() => currentHappiness;
        
        public CreatureState GetCurrentState() => currentState;
        public bool IsPerformingAction() => isPerformingAction;
        public bool IsAsleep() => isAsleep;
        
        /// <summary>
        /// Obtém estatísticas de cuidado para sistemas externos
        /// </summary>
        public (int feedings, int playSessions, int restSessions, int conversations, float careTime) GetCareStats()
        {
            return (totalFeedings, totalPlaySessions, totalRestSessions, totalConversations, totalCareTime);
        }
        
        void OnGUI()
        {
            if (showStatusInGUI)
            {
                GUI.Box(new Rect(10, 10, 200, 120), "Status da Criatura");
                GUI.Label(new Rect(20, 30, 180, 20), $"Fome: {currentHunger:F1}/{maxHunger}");
                GUI.Label(new Rect(20, 50, 180, 20), $"Energia: {currentEnergy:F1}/{maxEnergy}");
                GUI.Label(new Rect(20, 70, 180, 20), $"Felicidade: {currentHappiness:F1}/{maxHappiness}");
                GUI.Label(new Rect(20, 90, 180, 20), $"Estado: {currentState}");
                GUI.Label(new Rect(20, 110, 180, 20), $"Ação: {isPerformingAction}");
            }
        }
        
        void OnDestroy()
        {
            // Remove listeners para evitar vazamentos de memória
            if (aiResponder != null)
            {
                aiResponder.OnResponseReceived -= HandleAIResponse;
            }
            
            if (ttsManager != null)
            {
                ttsManager.OnSpeechStarted -= HandleSpeechStarted;
                ttsManager.OnSpeechCompleted -= HandleSpeechCompleted;
            }
        }
    }
}