using System;
using System.Collections;
using UnityEngine;

namespace Criamigos.Core
{
    /// <summary>
    /// Sistema de evolução da criatura que gerencia progressão, níveis e mudanças visuais
    /// Acompanha cuidados do jogador e desbloqueia novas aparências conforme a criatura cresce
    /// Integra com SaveSystem para persistir progresso entre sessões de jogo
    /// </summary>
    public class CreatureEvolution : MonoBehaviour
    {
        [Header("Configurações de Evolução")]
        [SerializeField] private int maxLevel = 10; // Nível máximo da criatura
        [SerializeField] private int currentLevel = 1; // Nível atual (inicia em 1)
        [SerializeField] private float currentExperience = 0f; // Experiência atual
        [SerializeField] private float baseExperienceRequired = 100f; // XP base para o nível 2
        [SerializeField] private float experienceMultiplier = 1.3f; // Multiplicador de XP por nível
        
        [Header("Ganho de Experiência")]
        [SerializeField] private float feedingXP = 15f; // XP por alimentação
        [SerializeField] private float playingXP = 20f; // XP por sessão de brincadeira
        [SerializeField] private float restingXP = 10f; // XP por descanso
        [SerializeField] private float conversationXP = 25f; // XP por conversa
        [SerializeField] private float careQualityMultiplier = 1.5f; // Bonus por cuidado de qualidade
        
        [Header("Aparências por Cor")]
        [SerializeField] private EvolutionStage[] blueCreatureStages; // Estágios da criatura azul
        [SerializeField] private EvolutionStage[] pinkCreatureStages; // Estágios da criatura rosa
        [SerializeField] private EvolutionStage[] greenCreatureStages; // Estágios da criatura verde
        [SerializeField] private EvolutionStage[] purpleCreatureStages; // Estágios da criatura roxa
        
        [Header("Sistema de Cores")]
        [SerializeField] private CreatureColor selectedColor = CreatureColor.Blue; // Cor selecionada
        [SerializeField] private bool allowColorChange = true; // Permite troca de cor
        [SerializeField] private int colorChangeLevel = 3; // Nível mínimo para trocar cor
        
        [Header("Efeitos Visuais")]
        [SerializeField] private GameObject evolutionEffectPrefab; // Efeito visual de evolução
        [SerializeField] private AudioClip evolutionSound; // Som de evolução
        [SerializeField] private float evolutionAnimationDuration = 3f; // Duração da animação de evolução
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true; // Logs detalhados
        [SerializeField] private bool showXPInGUI = false; // Mostra XP na tela
        
        // Enums e estruturas
        public enum CreatureColor { Blue, Pink, Green, Purple }
        public enum EvolutionTrigger { LevelUp, Manual, TimeBasked }
        
        [System.Serializable]
        public class EvolutionStage
        {
            public int requiredLevel; // Nível necessário para este estágio
            public GameObject creatureModel; // Modelo 3D desta evolução
            public string stageName; // Nome do estágio (Baby, Child, Teen, Adult)
            public string stageDescription; // Descrição para mostrar ao jogador
            public Sprite stageIcon; // Ícone para UI
            public Color themeColor = Color.white; // Cor tema deste estágio
            public bool isUnlocked = false; // Se o estágio foi desbloqueado
        }
        
        // Eventos públicos
        public event Action<int> OnLevelUp; // Disparado ao subir de nível
        public event Action<EvolutionStage> OnEvolutionComplete; // Disparado ao evoluir
        public event Action<float> OnExperienceGained; // Disparado ao ganhar XP
        public event Action<CreatureColor> OnColorChanged; // Disparado ao mudar cor
        
        // Estado interno
        private bool isReadyForEvolution = false;
        private bool isEvolving = false;
        private EvolutionStage currentStage;
        private SaveSystem saveSystem;
        
        // Estatísticas para cálculo de qualidade de cuidado
        private float lastCareQualityScore = 1f;
        private int consecutiveGoodCareDays = 0;
        
        void Awake()
        {
            // Busca o SaveSystem
            saveSystem = FindObjectOfType<SaveSystem>();
        }
        
        void Start()
        {
            // Carrega dados salvos se disponível
            LoadEvolutionData();
            
            // Aplica aparência inicial
            ApplyCurrentStage();
            
            if (enableDebugLogs)
                Debug.Log($"[CreatureEvolution] Sistema iniciado - Nível {currentLevel}, XP: {currentExperience:F1}");
        }
        
        /// <summary>
        /// Carrega dados de evolução do sistema de save
        /// </summary>
        private void LoadEvolutionData()
        {
            if (saveSystem == null) return;
            
            var saveData = saveSystem.LoadData();
            if (saveData != null)
            {
                currentLevel = saveData.creatureLevel;
                currentExperience = saveData.experience;
                selectedColor = (CreatureColor)saveData.selectedColor;
                consecutiveGoodCareDays = saveData.consecutiveGoodCareDays;
                
                // Aplica desbloqueios baseado no nível
                UnlockStagesUpToLevel(currentLevel);
                
                if (enableDebugLogs)
                    Debug.Log($"[CreatureEvolution] Dados carregados - Nível {currentLevel}, XP: {currentExperience}");
            }
        }
        
        /// <summary>
        /// Salva dados de evolução
        /// </summary>
        private void SaveEvolutionData()
        {
            if (saveSystem == null) return;
            
            var saveData = saveSystem.LoadData() ?? new SaveSystem.SaveData();
            
            saveData.creatureLevel = currentLevel;
            saveData.experience = currentExperience;
            saveData.selectedColor = (int)selectedColor;
            saveData.consecutiveGoodCareDays = consecutiveGoodCareDays;
            
            saveSystem.SaveData(saveData);
        }
        
        /// <summary>
        /// Adiciona experiência baseada em uma ação do jogador
        /// </summary>
        /// <param name="action">Tipo de ação realizada</param>
        /// <param name="careQuality">Qualidade do cuidado (0-1, onde 1 é perfeito)</param>
        public void GainExperience(CreatureController.CreatureAction action, float careQuality = 1f)
        {
            if (currentLevel >= maxLevel) return;
            
            float baseXP = GetBaseExperienceForAction(action);
            float qualityMultiplier = Mathf.Lerp(0.5f, careQualityMultiplier, careQuality);
            float finalXP = baseXP * qualityMultiplier;
            
            // Bonus para cuidado consistente
            if (consecutiveGoodCareDays > 3)
            {
                finalXP *= 1.2f; // 20% bonus
            }
            
            currentExperience += finalXP;
            OnExperienceGained?.Invoke(finalXP);
            
            if (enableDebugLogs)
                Debug.Log($"[CreatureEvolution] +{finalXP:F1} XP por {action} (Qualidade: {careQuality:F2})");
            
            // Verifica se subiu de nível
            CheckLevelUp();
            
            // Salva progresso
            SaveEvolutionData();
        }
        
        /// <summary>
        /// Obtém XP base para cada tipo de ação
        /// </summary>
        private float GetBaseExperienceForAction(CreatureController.CreatureAction action)
        {
            switch (action)
            {
                case CreatureController.CreatureAction.Eat: return feedingXP;
                case CreatureController.CreatureAction.Play: return playingXP;
                case CreatureController.CreatureAction.Sleep: return restingXP;
                case CreatureController.CreatureAction.Talk: return conversationXP;
                default: return 5f; // XP padrão para ações não listadas
            }
        }
        
        /// <summary>
        /// Calcula experiência necessária para o próximo nível
        /// </summary>
        public float GetExperienceRequiredForLevel(int level)
        {
            if (level <= 1) return 0f;
            
            float requiredXP = baseExperienceRequired;
            for (int i = 2; i < level; i++)
            {
                requiredXP *= experienceMultiplier;
            }
            
            return requiredXP;
        }
        
        /// <summary>
        /// Verifica se deve subir de nível
        /// </summary>
        private void CheckLevelUp()
        {
            float requiredXP = GetExperienceRequiredForLevel(currentLevel + 1);
            
            if (currentExperience >= requiredXP && currentLevel < maxLevel)
            {
                LevelUp();
            }
        }
        
        /// <summary>
        /// Realiza o processo de subir de nível
        /// </summary>
        private void LevelUp()
        {
            currentLevel++;
            
            // Remove XP usado para o nível
            float usedXP = GetExperienceRequiredForLevel(currentLevel);
            currentExperience -= usedXP;
            
            OnLevelUp?.Invoke(currentLevel);
            
            if (enableDebugLogs)
                Debug.Log($"[CreatureEvolution] Level Up! Novo nível: {currentLevel}");
            
            // Verifica se desbloqueou nova evolução
            CheckForNewEvolution();
            
            // Salva progresso
            SaveEvolutionData();
        }
        
        /// <summary>
        /// Verifica se desbloqueou uma nova forma evolutiva
        /// </summary>
        private void CheckForNewEvolution()
        {
            var availableStages = GetStagesForCurrentColor();
            
            foreach (var stage in availableStages)
            {
                if (stage.requiredLevel <= currentLevel && !stage.isUnlocked)
                {
                    UnlockEvolutionStage(stage);
                }
            }
        }
        
        /// <summary>
        /// Desbloqueia um estágio de evolução
        /// </summary>
        private void UnlockEvolutionStage(EvolutionStage stage)
        {
            stage.isUnlocked = true;
            
            // Se é o próximo estágio na sequência, oferece evolução
            if (stage.requiredLevel == currentLevel)
            {
                isReadyForEvolution = true;
                
                if (enableDebugLogs)
                    Debug.Log($"[CreatureEvolution] Nova evolução disponível: {stage.stageName}");
                
                // Pode evoluir automaticamente ou esperar comando do jogador
                StartCoroutine(DelayedEvolutionOffer(stage));
            }
        }
        
        /// <summary>
        /// Oferece evolução após um pequeno delay
        /// </summary>
        private IEnumerator DelayedEvolutionOffer(EvolutionStage stage)
        {
            yield return new WaitForSeconds(2f);
            
            // Aqui poderia mostrar UI perguntando se quer evoluir
            // Por enquanto, evolui automaticamente
            EvolveTo(stage);
        }
        
        /// <summary>
        /// Realiza a evolução para um estágio específico
        /// </summary>
        public void EvolveTo(EvolutionStage targetStage)
        {
            if (isEvolving || !targetStage.isUnlocked) return;
            
            StartCoroutine(EvolutionProcess(targetStage));
        }
        
        /// <summary>
        /// Processo completo de evolução com efeitos visuais
        /// </summary>
        private IEnumerator EvolutionProcess(EvolutionStage targetStage)
        {
            isEvolving = true;
            isReadyForEvolution = false;
            
            if (enableDebugLogs)
                Debug.Log($"[CreatureEvolution] Iniciando evolução para: {targetStage.stageName}");
            
            // Efeito visual de evolução
            GameObject evolutionEffect = null;
            if (evolutionEffectPrefab != null)
            {
                evolutionEffect = Instantiate(evolutionEffectPrefab, transform.position, Quaternion.identity);
            }
            
            // Som de evolução
            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null && evolutionSound != null)
            {
                audioSource.PlayOneShot(evolutionSound);
            }
            
            // Animação de evolução (fade out do modelo atual)
            yield return StartCoroutine(FadeOutCurrentModel());
            
            // Troca modelo
            ApplyEvolutionStage(targetStage);
            
            // Fade in do novo modelo
            yield return StartCoroutine(FadeInNewModel());
            
            // Limpa efeito visual
            if (evolutionEffect != null)
            {
                Destroy(evolutionEffect);
            }
            
            currentStage = targetStage;
            isEvolving = false;
            
            OnEvolutionComplete?.Invoke(targetStage);
            
            if (enableDebugLogs)
                Debug.Log($"[CreatureEvolution] Evolução completa: {targetStage.stageName}");
            
            // Salva estado
            SaveEvolutionData();
        }
        
        /// <summary>
        /// Aplica a aparência de um estágio de evolução
        /// </summary>
        private void ApplyEvolutionStage(EvolutionStage stage)
        {
            // Desativa modelos atuais
            DeactivateAllModels();
            
            // Ativa novo modelo
            if (stage.creatureModel != null)
            {
                stage.creatureModel.SetActive(true);
                currentStage = stage;
            }
        }
        
        /// <summary>
        /// Aplica estágio atual baseado no nível
        /// </summary>
        private void ApplyCurrentStage()
        {
            var availableStages = GetStagesForCurrentColor();
            EvolutionStage stageToApply = null;
            
            // Encontra o estágio apropriado para o nível atual
            for (int i = availableStages.Length - 1; i >= 0; i--)
            {
                if (availableStages[i].requiredLevel <= currentLevel)
                {
                    stageToApply = availableStages[i];
                    break;
                }
            }
            
            if (stageToApply != null)
            {
                ApplyEvolutionStage(stageToApply);
            }
        }
        
        /// <summary>
        /// Obtém estágios disponíveis para a cor atual
        /// </summary>
        private EvolutionStage[] GetStagesForCurrentColor()
        {
            switch (selectedColor)
            {
                case CreatureColor.Blue: return blueCreatureStages;
                case CreatureColor.Pink: return pinkCreatureStages;
                case CreatureColor.Green: return greenCreatureStages;
                case CreatureColor.Purple: return purpleCreatureStages;
                default: return blueCreatureStages;
            }
        }
        
        /// <summary>
        /// Desativa todos os modelos de criatura
        /// </summary>
        private void DeactivateAllModels()
        {
            DeactivateStages(blueCreatureStages);
            DeactivateStages(pinkCreatureStages);
            DeactivateStages(greenCreatureStages);
            DeactivateStages(purpleCreatureStages);
        }
        
        private void DeactivateStages(EvolutionStage[] stages)
        {
            foreach (var stage in stages)
            {
                if (stage.creatureModel != null)
                {
                    stage.creatureModel.SetActive(false);
                }
            }
        }
        
        /// <summary>
        /// Desbloqueia todos os estágios até um nível específico
        /// </summary>
        private void UnlockStagesUpToLevel(int level)
        {
            var allStages = new EvolutionStage[][]
            {
                blueCreatureStages, pinkCreatureStages,
                greenCreatureStages, purpleCreatureStages
            };
            
            foreach (var stageArray in allStages)
            {
                foreach (var stage in stageArray)
                {
                    if (stage.requiredLevel <= level)
                    {
                        stage.isUnlocked = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// Muda a cor da criatura
        /// </summary>
        public bool ChangeColor(CreatureColor newColor)
        {
            if (!allowColorChange || currentLevel < colorChangeLevel)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[CreatureEvolution] Troca de cor não permitida. Nível atual: {currentLevel}, Necessário: {colorChangeLevel}");
                return false;
            }
            
            if (selectedColor == newColor) return false;
            
            selectedColor = newColor;
            OnColorChanged?.Invoke(newColor);
            
            // Aplica nova aparência
            ApplyCurrentStage();
            
            // Salva mudança
            SaveEvolutionData();
            
            if (enableDebugLogs)
                Debug.Log($"[CreatureEvolution] Cor alterada para: {newColor}");
            
            return true;
        }
        
        /// <summary>
        /// Atualiza qualidade de cuidado (chamado diariamente)
        /// </summary>
        public void UpdateCareQuality(float dailyCareScore)
        {
            lastCareQualityScore = dailyCareScore;
            
            if (dailyCareScore >= 0.8f)
            {
                consecutiveGoodCareDays++;
            }
            else
            {
                consecutiveGoodCareDays = 0;
            }
            
            SaveEvolutionData();
        }
        
        // Efeitos visuais para evolução
        
        private IEnumerator FadeOutCurrentModel()
        {
            float fadeTime = evolutionAnimationDuration * 0.3f;
            float elapsedTime = 0f;
            
            while (elapsedTime < fadeTime)
            {
                float alpha = 1f - (elapsedTime / fadeTime);
                SetModelAlpha(alpha);
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            SetModelAlpha(0f);
        }
        
        private IEnumerator FadeInNewModel()
        {
            float fadeTime = evolutionAnimationDuration * 0.4f;
            float elapsedTime = 0f;
            
            SetModelAlpha(0f);
            
            while (elapsedTime < fadeTime)
            {
                float alpha = elapsedTime / fadeTime;
                SetModelAlpha(alpha);
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            SetModelAlpha(1f);
        }
        
        private void SetModelAlpha(float alpha)
        {
            if (currentStage?.creatureModel == null) return;
            
            var renderers = currentStage.creatureModel.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var materials = renderer.materials;
                foreach (var material in materials)
                {
                    if (material.HasProperty("_Color"))
                    {
                        var color = material.color;
                        color.a = alpha;
                        material.color = color;
                    }
                }
            }
        }
        
        // Métodos públicos de informação
        
        public int GetCurrentLevel() => currentLevel;
        public float GetCurrentExperience() => currentExperience;
        public float GetExperienceToNextLevel() => GetExperienceRequiredForLevel(currentLevel + 1) - currentExperience;
        public float GetProgressToNextLevel() => currentExperience / GetExperienceRequiredForLevel(currentLevel + 1);
        public bool IsEvolutionReady() => isReadyForEvolution;
        public bool IsEvolving() => isEvolving;
        public CreatureColor GetCurrentColor() => selectedColor;
        public EvolutionStage GetCurrentStage() => currentStage;
        public int GetConsecutiveGoodCareDays() => consecutiveGoodCareDays;
        
        /// <summary>
        /// Define se está pronto para evolução (usado pelo CreatureController)
        /// </summary>
        public void SetReadyForEvolution(bool ready)
        {
            isReadyForEvolution = ready;
        }
        
        /// <summary>
        /// Obtém todas as cores disponíveis
        /// </summary>
        public CreatureColor[] GetAvailableColors()
        {
            return new CreatureColor[] { CreatureColor.Blue, CreatureColor.Pink, CreatureColor.Green, CreatureColor.Purple };
        }
        
        /// <summary>
        /// Verifica se uma cor está disponível
        /// </summary>
        public bool IsColorAvailable(CreatureColor color)
        {
            return currentLevel >= colorChangeLevel;
        }
        
        /// <summary>
        /// Obtém informações de progresso para UI
        /// </summary>
        public string GetProgressInfo()
        {
            if (currentLevel >= maxLevel)
            {
                return "Nível máximo atingido!";
            }
            
            float remainingXP = GetExperienceToNextLevel();
            return $"Nível {currentLevel} - {remainingXP:F0} XP para o próximo nível";
        }
        
        void OnGUI()
        {
            if (showXPInGUI)
            {
                GUI.Box(new Rect(220, 10, 200, 100), "Evolução");
                GUI.Label(new Rect(230, 30, 180, 20), $"Nível: {currentLevel}/{maxLevel}");
                GUI.Label(new Rect(230, 50, 180, 20), $"XP: {currentExperience:F0}");
                GUI.Label(new Rect(230, 70, 180, 20), $"Para próximo: {GetExperienceToNextLevel():F0}");
                GUI.Label(new Rect(230, 90, 180, 20), $"Cor: {selectedColor}");
            }
        }
    }
}