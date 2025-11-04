using System;
using UnityEngine;

/// <summary>
/// Sistema que gerencia os valores de fome, energia e felicidade da criatura
/// Valores diminuem automaticamente com o tempo e podem ser restaurados através de ações
/// Envia eventos quando os valores mudam para outros sistemas reagirem
/// </summary>
public class HungerSystem : MonoBehaviour
{
    [Header("Configurações de Fome")]
    [SerializeField] private float maxHunger = 100f; // Valor máximo que a fome pode ter
    [SerializeField] private float currentHunger = 100f; // Valor atual da fome (0 = muito com fome, 100 = satisfeito)
    [SerializeField] private float hungerDecayRate = 5f; // Quanto de fome diminui por minuto
    
    [Header("Configurações de Energia")]
    [SerializeField] private float maxEnergy = 100f; // Valor máximo que a energia pode ter
    [SerializeField] private float currentEnergy = 100f; // Valor atual da energia (0 = cansado, 100 = descansado)
    [SerializeField] private float energyDecayRate = 3f; // Quanto de energia diminui por minuto
    
    [Header("Configurações de Felicidade")]
    [SerializeField] private float maxHappiness = 100f; // Valor máximo que a felicidade pode ter
    [SerializeField] private float currentHappiness = 100f; // Valor atual da felicidade (0 = triste, 100 = alegre)
    [SerializeField] private float happinessDecayRate = 2f; // Quanto de felicidade diminui por minuto
    
    [Header("Limites Críticos")]
    [SerializeField] private float criticalThreshold = 20f; // Abaixo deste valor, a criatura fica em estado crítico
    
    // Eventos que outros scripts podem escutar quando os valores mudam
    public event Action<float, float> OnHungerChanged; // Passa valor atual e valor normalizado (0-1)
    public event Action<float, float> OnEnergyChanged; // Passa valor atual e valor normalizado (0-1)  
    public event Action<float, float> OnHappinessChanged; // Passa valor atual e valor normalizado (0-1)
    public event Action OnCriticalState; // Disparado quando algum valor fica crítico
    public event Action OnHealthyState; // Disparado quando todos os valores estão saudáveis
    
    private bool isInCriticalState = false; // Controla se já estamos em estado crítico
    
    void Start()
    {
        // Inicializa os valores com os máximos configurados
        currentHunger = maxHunger;
        currentEnergy = maxEnergy;
        currentHappiness = maxHappiness;
        
        // Dispara os eventos iniciais para atualizar a UI
        OnHungerChanged?.Invoke(currentHunger, GetHungerNormalized());
        OnEnergyChanged?.Invoke(currentEnergy, GetEnergyNormalized());
        OnHappinessChanged?.Invoke(currentHappiness, GetHappinessNormalized());
        
        // Inicia o processo de diminuição automática dos valores a cada minuto
        InvokeRepeating(nameof(DecayValues), 60f, 60f);
    }
    
    /// <summary>
    /// Método chamado automaticamente a cada minuto para diminuir os valores
    /// </summary>
    private void DecayValues()
    {
        // Diminui a fome baseado na taxa configurada
        currentHunger -= hungerDecayRate;
        if (currentHunger < 0f) currentHunger = 0f; // Não deixa ficar negativo
        
        // Diminui a energia baseado na taxa configurada  
        currentEnergy -= energyDecayRate;
        if (currentEnergy < 0f) currentEnergy = 0f; // Não deixa ficar negativo
        
        // Diminui a felicidade baseado na taxa configurada
        currentHappiness -= happinessDecayRate;
        if (currentHappiness < 0f) currentHappiness = 0f; // Não deixa ficar negativo
        
        // Dispara os eventos para notificar outros sistemas sobre as mudanças
        OnHungerChanged?.Invoke(currentHunger, GetHungerNormalized());
        OnEnergyChanged?.Invoke(currentEnergy, GetEnergyNormalized());
        OnHappinessChanged?.Invoke(currentHappiness, GetHappinessNormalized());
        
        // Verifica se a criatura entrou ou saiu do estado crítico
        CheckCriticalState();
    }
    
    /// <summary>
    /// Verifica se algum valor está abaixo do limite crítico
    /// </summary>
    private void CheckCriticalState()
    {
        bool shouldBeInCriticalState = currentHunger <= criticalThreshold || 
                                      currentEnergy <= criticalThreshold || 
                                      currentHappiness <= criticalThreshold;
        
        // Se mudou de estado, dispara o evento apropriado
        if (shouldBeInCriticalState && !isInCriticalState)
        {
            isInCriticalState = true;
            OnCriticalState?.Invoke(); // Notifica que entrou em estado crítico
        }
        else if (!shouldBeInCriticalState && isInCriticalState)
        {
            isInCriticalState = false;
            OnHealthyState?.Invoke(); // Notifica que saiu do estado crítico
        }
    }
    
    /// <summary>
    /// Alimenta a criatura, aumentando a fome
    /// </summary>
    /// <param name="amount">Quantidade de fome a ser restaurada</param>
    public void Feed(float amount)
    {
        currentHunger += amount; // Adiciona a quantidade especificada
        if (currentHunger > maxHunger) currentHunger = maxHunger; // Não deixa passar do máximo
        
        OnHungerChanged?.Invoke(currentHunger, GetHungerNormalized()); // Notifica a mudança
        CheckCriticalState(); // Verifica se saiu do estado crítico
    }
    
    /// <summary>
    /// Faz a criatura descansar, aumentando a energia
    /// </summary>
    /// <param name="amount">Quantidade de energia a ser restaurada</param>
    public void Rest(float amount)
    {
        currentEnergy += amount; // Adiciona a quantidade especificada
        if (currentEnergy > maxEnergy) currentEnergy = maxEnergy; // Não deixa passar do máximo
        
        OnEnergyChanged?.Invoke(currentEnergy, GetEnergyNormalized()); // Notifica a mudança
        CheckCriticalState(); // Verifica se saiu do estado crítico
    }
    
    /// <summary>
    /// Brinca com a criatura, aumentando a felicidade
    /// </summary>
    /// <param name="amount">Quantidade de felicidade a ser restaurada</param>
    public void Play(float amount)
    {
        currentHappiness += amount; // Adiciona a quantidade especificada
        if (currentHappiness > maxHappiness) currentHappiness = maxHappiness; // Não deixa passar do máximo
        
        OnHappinessChanged?.Invoke(currentHappiness, GetHappinessNormalized()); // Notifica a mudança
        CheckCriticalState(); // Verifica se saiu do estado crítico
    }
    
    // Métodos para obter valores normalizados (0-1) - útil para UI sliders
    
    /// <summary>
    /// Retorna a fome normalizada entre 0 e 1 (0 = fome máxima, 1 = satisfeito)
    /// </summary>
    public float GetHungerNormalized()
    {
        return currentHunger / maxHunger;
    }
    
    /// <summary>
    /// Retorna a energia normalizada entre 0 e 1 (0 = sem energia, 1 = cheio de energia)
    /// </summary>
    public float GetEnergyNormalized()
    {
        return currentEnergy / maxEnergy;
    }
    
    /// <summary>
    /// Retorna a felicidade normalizada entre 0 e 1 (0 = triste, 1 = muito feliz)
    /// </summary>
    public float GetHappinessNormalized()
    {
        return currentHappiness / maxHappiness;
    }
    
    // Métodos para obter valores absolutos
    
    /// <summary>
    /// Retorna o valor atual da fome
    /// </summary>
    public float GetHunger()
    {
        return currentHunger;
    }
    
    /// <summary>
    /// Retorna o valor atual da energia
    /// </summary>
    public float GetEnergy()
    {
        return currentEnergy;
    }
    
    /// <summary>
    /// Retorna o valor atual da felicidade
    /// </summary>
    public float GetHappiness()
    {
        return currentHappiness;
    }
    
    /// <summary>
    /// Verifica se a criatura está em estado crítico
    /// </summary>
    public bool IsInCriticalState()
    {
        return isInCriticalState;
    }
    
    /// <summary>
    /// Restaura todos os valores para o máximo (útil para testes ou power-ups especiais)
    /// </summary>
    public void RestoreAll()
    {
        currentHunger = maxHunger;
        currentEnergy = maxEnergy;
        currentHappiness = maxHappiness;
        
        // Dispara todos os eventos de mudança
        OnHungerChanged?.Invoke(currentHunger, GetHungerNormalized());
        OnEnergyChanged?.Invoke(currentEnergy, GetEnergyNormalized());
        OnHappinessChanged?.Invoke(currentHappiness, GetHappinessNormalized());
        
        CheckCriticalState(); // Verifica o estado crítico
    }
}