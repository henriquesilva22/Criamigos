using UnityEngine;

/// <summary>
/// Controlador principal da criatura que conecta todos os sistemas
/// Gerencia animações baseadas no estado da criatura e coordena respostas a interações
/// Atua como ponte entre HungerSystem, UI, Audio e outros componentes
/// </summary>
[RequireComponent(typeof(Animator))]
public class CreatureController : MonoBehaviour
{
    [Header("Componentes Requeridos")]
    [SerializeField] private Animator animator; // Animator que controla as animações da criatura
    [SerializeField] private HungerSystem hungerSystem; // Referência para o sistema de fome/energia/felicidade
    [SerializeField] private AudioSource audioSource; // AudioSource para tocar sons da criatura
    
    [Header("Configurações de Áudio")]
    [SerializeField] private AudioClip happySound; // Som quando a criatura está feliz
    [SerializeField] private AudioClip sadSound; // Som quando a criatura está triste
    [SerializeField] private AudioClip hungrySound; // Som quando a criatura está com fome
    [SerializeField] private AudioClip tiredSound; // Som quando a criatura está cansada
    [SerializeField] private AudioClip eatSound; // Som quando a criatura come
    [SerializeField] private AudioClip playSound; // Som quando a criatura brinca
    
    [Header("Configurações de Animação")]
    [SerializeField] private string hungerParam = "Hunger"; // Nome do parâmetro de fome no Animator
    [SerializeField] private string energyParam = "Energy"; // Nome do parâmetro de energia no Animator
    [SerializeField] private string happinessParam = "Happiness"; // Nome do parâmetro de felicidade no Animator
    [SerializeField] private string eatTrigger = "Eat"; // Nome do trigger de comer no Animator
    [SerializeField] private string playTrigger = "Play"; // Nome do trigger de brincar no Animator
    [SerializeField] private string restTrigger = "Rest"; // Nome do trigger de descansar no Animator
    [SerializeField] private string criticalTrigger = "Critical"; // Nome do trigger de estado crítico no Animator
    
    [Header("Configurações de Comportamento")]
    [SerializeField] private float soundCooldown = 2f; // Tempo mínimo entre sons automáticos
    [SerializeField] private float animationBlendSpeed = 2f; // Velocidade de transição entre animações
    
    private float lastSoundTime = 0f; // Controla quando o último som foi tocado
    private bool wasInCriticalState = false; // Controla se estava em estado crítico no frame anterior
    
    void Awake()
    {
        // Se não foi atribuído no Inspector, pega o Animator deste GameObject
        if (animator == null)
            animator = GetComponent<Animator>();
        
        // Se não foi atribuído no Inspector, pega o AudioSource deste GameObject  
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }
    
    void Start()
    {
        // Verifica se os componentes essenciais estão configurados
        if (hungerSystem == null)
        {
            Debug.LogError("HungerSystem não foi atribuído no CreatureController!");
            return;
        }
        
        if (animator == null)
        {
            Debug.LogError("Animator não foi encontrado no CreatureController!");
            return;
        }
        
        // Configura os valores iniciais do Animator baseado no HungerSystem
        UpdateAnimatorParameters();
    }
    
    void OnEnable()
    {
        // Se inscreve nos eventos do HungerSystem quando o objeto fica ativo
        if (hungerSystem != null)
        {
            hungerSystem.OnHungerChanged += HandleHungerChanged;
            hungerSystem.OnEnergyChanged += HandleEnergyChanged;
            hungerSystem.OnHappinessChanged += HandleHappinessChanged;
            hungerSystem.OnCriticalState += HandleCriticalState;
            hungerSystem.OnHealthyState += HandleHealthyState;
        }
    }
    
    void OnDisable()
    {
        // Se desinscreve dos eventos quando o objeto fica inativo
        if (hungerSystem != null)
        {
            hungerSystem.OnHungerChanged -= HandleHungerChanged;
            hungerSystem.OnEnergyChanged -= HandleEnergyChanged;
            hungerSystem.OnHappinessChanged -= HandleHappinessChanged;
            hungerSystem.OnCriticalState -= HandleCriticalState;
            hungerSystem.OnHealthyState -= HandleHealthyState;
        }
    }
    
    /// <summary>
    /// Atualiza todos os parâmetros do Animator com os valores atuais
    /// </summary>
    private void UpdateAnimatorParameters()
    {
        if (animator != null && hungerSystem != null)
        {
            // Atualiza os parâmetros float do Animator com valores normalizados (0-1)
            animator.SetFloat(hungerParam, hungerSystem.GetHungerNormalized());
            animator.SetFloat(energyParam, hungerSystem.GetEnergyNormalized());
            animator.SetFloat(happinessParam, hungerSystem.GetHappinessNormalized());
        }
    }
    
    /// <summary>
    /// Chamado quando a fome da criatura muda
    /// </summary>
    private void HandleHungerChanged(float hungerValue, float normalizedValue)
    {
        // Atualiza o parâmetro de fome no Animator
        if (animator != null)
            animator.SetFloat(hungerParam, normalizedValue);
        
        // Toca som de fome se estiver muito baixa e não tocou som recentemente
        if (normalizedValue < 0.3f && CanPlaySound())
        {
            PlayCreatureSound(hungrySound);
        }
    }
    
    /// <summary>
    /// Chamado quando a energia da criatura muda
    /// </summary>
    private void HandleEnergyChanged(float energyValue, float normalizedValue)
    {
        // Atualiza o parâmetro de energia no Animator
        if (animator != null)
            animator.SetFloat(energyParam, normalizedValue);
        
        // Toca som de cansaço se estiver muito baixa e não tocou som recentemente
        if (normalizedValue < 0.3f && CanPlaySound())
        {
            PlayCreatureSound(tiredSound);
        }
    }
    
    /// <summary>
    /// Chamado quando a felicidade da criatura muda
    /// </summary>
    private void HandleHappinessChanged(float happinessValue, float normalizedValue)
    {
        // Atualiza o parâmetro de felicidade no Animator
        if (animator != null)
            animator.SetFloat(happinessParam, normalizedValue);
        
        // Toca som apropriado baseado no nível de felicidade
        if (normalizedValue > 0.8f && CanPlaySound())
        {
            PlayCreatureSound(happySound); // Som de felicidade quando muito feliz
        }
        else if (normalizedValue < 0.3f && CanPlaySound())
        {
            PlayCreatureSound(sadSound); // Som de tristeza quando muito triste
        }
    }
    
    /// <summary>
    /// Chamado quando a criatura entra em estado crítico
    /// </summary>
    private void HandleCriticalState()
    {
        Debug.Log("Criatura entrou em estado crítico!");
        
        // Dispara animação de estado crítico
        if (animator != null)
            animator.SetTrigger(criticalTrigger);
        
        // Toca som de alerta
        PlayCreatureSound(sadSound);
        
        wasInCriticalState = true;
    }
    
    /// <summary>
    /// Chamado quando a criatura sai do estado crítico
    /// </summary>
    private void HandleHealthyState()
    {
        Debug.Log("Criatura voltou ao estado saudável!");
        
        // Toca som de alívio/felicidade
        PlayCreatureSound(happySound);
        
        wasInCriticalState = false;
    }
    
    /// <summary>
    /// Verifica se pode tocar um som baseado no cooldown
    /// </summary>
    private bool CanPlaySound()
    {
        return Time.time - lastSoundTime >= soundCooldown;
    }
    
    /// <summary>
    /// Toca um som da criatura se o AudioSource estiver disponível
    /// </summary>
    private void PlayCreatureSound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip); // Toca o som sem interromper outros sons
            lastSoundTime = Time.time; // Atualiza o tempo do último som tocado
        }
    }
    
    // Métodos públicos para serem chamados pela UI ou outros sistemas
    
    /// <summary>
    /// Alimenta a criatura - chamado por botões da UI
    /// </summary>
    public void FeedCreature()
    {
        if (hungerSystem != null)
        {
            hungerSystem.Feed(25f); // Restaura 25 pontos de fome
            
            // Dispara animação de comer
            if (animator != null)
                animator.SetTrigger(eatTrigger);
            
            // Toca som de comer
            PlayCreatureSound(eatSound);
            
            Debug.Log("Criatura foi alimentada!");
        }
    }
    
    /// <summary>
    /// Faz a criatura descansar - chamado por botões da UI
    /// </summary>
    public void RestCreature()
    {
        if (hungerSystem != null)
        {
            hungerSystem.Rest(30f); // Restaura 30 pontos de energia
            
            // Dispara animação de descansar
            if (animator != null)
                animator.SetTrigger(restTrigger);
            
            Debug.Log("Criatura descansou!");
        }
    }
    
    /// <summary>
    /// Brinca com a criatura - chamado por botões da UI
    /// </summary>
    public void PlayWithCreature()
    {
        if (hungerSystem != null)
        {
            hungerSystem.Play(20f); // Restaura 20 pontos de felicidade
            
            // Dispara animação de brincar
            if (animator != null)
                animator.SetTrigger(playTrigger);
            
            // Toca som de brincadeira
            PlayCreatureSound(playSound);
            
            Debug.Log("Brincou com a criatura!");
        }
    }
    
    /// <summary>
    /// Restaura todos os valores da criatura - útil para testes ou power-ups
    /// </summary>
    public void RestoreAllNeeds()
    {
        if (hungerSystem != null)
        {
            hungerSystem.RestoreAll();
            PlayCreatureSound(happySound);
            Debug.Log("Todas as necessidades da criatura foram restauradas!");
        }
    }
    
    /// <summary>
    /// Retorna se a criatura está em estado crítico
    /// </summary>
    public bool IsCreatureInCriticalState()
    {
        return hungerSystem != null && hungerSystem.IsInCriticalState();
    }
    
    /// <summary>
    /// Método para fazer a criatura reagir a comandos de voz ou interações especiais
    /// </summary>
    public void ReactToInteraction(string interactionType)
    {
        switch (interactionType.ToLower())
        {
            case "happy":
            case "feliz":
                PlayCreatureSound(happySound);
                break;
            case "sad":
            case "triste":
                PlayCreatureSound(sadSound);
                break;
            case "hungry":
            case "fome":
                PlayCreatureSound(hungrySound);
                break;
            case "tired":
            case "cansado":
                PlayCreatureSound(tiredSound);
                break;
            default:
                Debug.Log($"Reação não reconhecida: {interactionType}");
                break;
        }
    }
}