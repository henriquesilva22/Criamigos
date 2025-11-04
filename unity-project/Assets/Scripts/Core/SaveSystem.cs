using System;
using System.IO;
using UnityEngine;

namespace Criamigos.Core
{
    /// <summary>
    /// Sistema de persistência de dados que salva progresso da criatura em formato JSON
    /// Gerencia dados locais de forma segura e eficiente entre sessões de jogo
    /// Inclui sistema de backup e recuperação em caso de dados corrompidos
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        [Header("Configurações de Save")]
        [SerializeField] private string saveFileName = "criamigos_save.json"; // Nome do arquivo de save
        [SerializeField] private bool enableAutoSave = true; // Salvamento automático
        [SerializeField] private float autoSaveInterval = 300f; // Intervalo de auto-save (5 minutos)
        [SerializeField] private bool enableBackup = true; // Sistema de backup
        [SerializeField] private int maxBackupFiles = 3; // Máximo de arquivos de backup
        
        [Header("Criptografia (Opcional)")]
        [SerializeField] private bool enableEncryption = false; // Criptografia simples dos dados
        [SerializeField] private string encryptionKey = "CriamigosKey2024"; // Chave de criptografia
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true; // Logs detalhados
        [SerializeField] private bool showSaveInfoInGUI = false; // Mostra informações de save na tela
        
        // Eventos públicos para notificar outros sistemas
        public event Action<SaveData> OnDataSaved; // Disparado quando dados são salvos
        public event Action<SaveData> OnDataLoaded; // Disparado quando dados são carregados
        public event Action<string> OnSaveError; // Disparado quando há erro no save
        
        // Caminhos de arquivo
        private string saveFilePath;
        private string backupDirectory;
        
        // Estado do sistema
        private SaveData currentSaveData;
        private float lastSaveTime = 0f;
        private bool hasUnsavedChanges = false;
        
        /// <summary>
        /// Estrutura principal de dados salvos
        /// </summary>
        [System.Serializable]
        public class SaveData
        {
            [Header("Informações Gerais")]
            public string playerName = "Criança"; // Nome do jogador
            public long saveTimestamp; // Timestamp do último save
            public int saveVersion = 1; // Versão do formato de save
            public float totalPlayTime = 0f; // Tempo total de jogo em horas
            
            [Header("Estado da Criatura")]
            public float hungerLevel = 80f; // Nível de fome atual
            public float energyLevel = 80f; // Nível de energia atual
            public float happinessLevel = 80f; // Nível de felicidade atual
            public bool isAsleep = false; // Se a criatura está dormindo
            
            [Header("Progresso e Evolução")]
            public int creatureLevel = 1; // Nível da criatura
            public float experience = 0f; // Experiência atual
            public int selectedColor = 0; // Cor selecionada (enum como int)
            public bool[] unlockedStages = new bool[10]; // Estágios desbloqueados
            
            [Header("Estatísticas de Cuidado")]
            public int totalFeedings = 0; // Total de alimentações
            public int totalPlaySessions = 0; // Total de sessões de brincadeira
            public int totalRestSessions = 0; // Total de descansos
            public int totalConversations = 0; // Total de conversas
            public int consecutiveGoodCareDays = 0; // Dias consecutivos de bom cuidado
            
            [Header("Configurações do Jogador")]
            public bool soundEnabled = true; // Som habilitado
            public bool musicEnabled = true; // Música habilitada
            public float masterVolume = 1f; // Volume principal
            public string preferredLanguage = "pt-BR"; // Idioma preferido
            
            [Header("Dados de Sessão")]
            public long lastPlayTimestamp; // Última vez que jogou
            public int daysPlayed = 1; // Dias que jogou o jogo
            public float longestPlaySession = 0f; // Sessão mais longa em horas
            
            public SaveData()
            {
                // Inicializa arrays e valores padrão
                unlockedStages = new bool[10];
                unlockedStages[0] = true; // Primeira evolução sempre desbloqueada
                saveTimestamp = GetCurrentTimestamp();
                lastPlayTimestamp = saveTimestamp;
            }
            
            /// <summary>
            /// Obtém timestamp atual
            /// </summary>
            public static long GetCurrentTimestamp()
            {
                return DateTimeOffset.Now.ToUnixTimeSeconds();
            }
        }
        
        void Awake()
        {
            // Configura caminhos de arquivo
            SetupFilePaths();
            
            // Garante que é singleton
            var existingSaveSystem = FindObjectOfType<SaveSystem>();
            if (existingSaveSystem != null && existingSaveSystem != this)
            {
                Destroy(gameObject);
                return;
            }
            
            // Não destrói ao carregar novas cenas
            DontDestroyOnLoad(gameObject);
        }
        
        void Start()
        {
            // Carrega dados automaticamente
            LoadData();
            
            // Inicia auto-save se habilitado
            if (enableAutoSave)
            {
                InvokeRepeating(nameof(AutoSave), autoSaveInterval, autoSaveInterval);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[SaveSystem] Sistema iniciado - Arquivo: {saveFilePath}");
        }
        
        /// <summary>
        /// Configura caminhos de arquivos baseado na plataforma
        /// </summary>
        private void SetupFilePaths()
        {
            string saveDirectory = Application.persistentDataPath;
            
            // Cria diretório específico para o jogo
            saveDirectory = Path.Combine(saveDirectory, "Criamigos");
            
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }
            
            saveFilePath = Path.Combine(saveDirectory, saveFileName);
            backupDirectory = Path.Combine(saveDirectory, "Backups");
            
            if (enableBackup && !Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }
        }
        
        /// <summary>
        /// Salva dados atuais no arquivo
        /// </summary>
        public bool SaveData(SaveData dataToSave = null)
        {
            try
            {
                // Usa dados fornecidos ou dados atuais
                SaveData saveData = dataToSave ?? currentSaveData ?? new SaveData();
                
                // Atualiza informações de tempo
                saveData.saveTimestamp = SaveData.GetCurrentTimestamp();
                
                // Cria backup antes de salvar
                if (enableBackup && File.Exists(saveFilePath))
                {
                    CreateBackup();
                }
                
                // Converte para JSON
                string jsonData = JsonUtility.ToJson(saveData, true);
                
                // Aplica criptografia se habilitada
                if (enableEncryption)
                {
                    jsonData = SimpleEncrypt(jsonData);
                }
                
                // Escreve arquivo
                File.WriteAllText(saveFilePath, jsonData);
                
                // Atualiza estado interno
                currentSaveData = saveData;
                lastSaveTime = Time.time;
                hasUnsavedChanges = false;
                
                // Notifica outros sistemas
                OnDataSaved?.Invoke(saveData);
                
                if (enableDebugLogs)
                    Debug.Log($"[SaveSystem] Dados salvos com sucesso");
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Erro ao salvar dados: {e.Message}");
                OnSaveError?.Invoke($"Erro ao salvar: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Carrega dados do arquivo
        /// </summary>
        public SaveData LoadData()
        {
            try
            {
                if (!File.Exists(saveFilePath))
                {
                    if (enableDebugLogs)
                        Debug.Log("[SaveSystem] Arquivo de save não encontrado, criando novo");
                    
                    // Cria dados padrão se arquivo não existe
                    currentSaveData = new SaveData();
                    SaveData(currentSaveData);
                    return currentSaveData;
                }
                
                // Lê arquivo
                string jsonData = File.ReadAllText(saveFilePath);
                
                // Remove criptografia se habilitada
                if (enableEncryption)
                {
                    jsonData = SimpleDecrypt(jsonData);
                }
                
                // Converte de JSON
                SaveData loadedData = JsonUtility.FromJson<SaveData>(jsonData);
                
                // Valida dados carregados
                if (ValidateLoadedData(loadedData))
                {
                    currentSaveData = loadedData;
                    
                    // Atualiza tempo de sessão
                    UpdateSessionData();
                    
                    OnDataLoaded?.Invoke(currentSaveData);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[SaveSystem] Dados carregados - Nível {currentSaveData.creatureLevel}, Tempo total: {currentSaveData.totalPlayTime:F1}h");
                    
                    return currentSaveData;
                }
                else
                {
                    throw new Exception("Dados carregados são inválidos");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Erro ao carregar dados: {e.Message}");
                
                // Tenta restaurar do backup
                if (enableBackup && TryRestoreFromBackup())
                {
                    return currentSaveData;
                }
                
                // Se falhar, cria dados novos
                Debug.LogWarning("[SaveSystem] Criando novos dados após falha");
                currentSaveData = new SaveData();
                SaveData(currentSaveData);
                
                OnSaveError?.Invoke($"Erro ao carregar, dados resetados: {e.Message}");
                return currentSaveData;
            }
        }
        
        /// <summary>
        /// Valida dados carregados para detectar corrupção
        /// </summary>
        private bool ValidateLoadedData(SaveData data)
        {
            if (data == null) return false;
            
            // Validações básicas
            if (data.creatureLevel < 1 || data.creatureLevel > 20) return false;
            if (data.hungerLevel < 0 || data.hungerLevel > 200) return false;
            if (data.energyLevel < 0 || data.energyLevel > 200) return false;
            if (data.happinessLevel < 0 || data.happinessLevel > 200) return false;
            if (data.experience < 0) return false;
            if (data.totalPlayTime < 0) return false;
            
            // Verifica se arrays estão inicializados
            if (data.unlockedStages == null)
            {
                data.unlockedStages = new bool[10];
                data.unlockedStages[0] = true;
            }
            
            return true;
        }
        
        /// <summary>
        /// Atualiza dados de sessão de jogo
        /// </summary>
        private void UpdateSessionData()
        {
            if (currentSaveData == null) return;
            
            long currentTime = SaveData.GetCurrentTimestamp();
            long timeDifference = currentTime - currentSaveData.lastPlayTimestamp;
            
            // Se passou mais de um dia, incrementa contador de dias
            if (timeDifference > 86400) // 24 horas em segundos
            {
                currentSaveData.daysPlayed++;
                
                // Reset de cuidado consecutivo se passou muito tempo
                if (timeDifference > 172800) // 48 horas
                {
                    currentSaveData.consecutiveGoodCareDays = 0;
                }
            }
            
            currentSaveData.lastPlayTimestamp = currentTime;
        }
        
        /// <summary>
        /// Cria backup do arquivo atual
        /// </summary>
        private void CreateBackup()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"backup_{timestamp}.json";
                string backupPath = Path.Combine(backupDirectory, backupFileName);
                
                File.Copy(saveFilePath, backupPath);
                
                // Remove backups antigos se exceder o limite
                CleanupOldBackups();
                
                if (enableDebugLogs)
                    Debug.Log($"[SaveSystem] Backup criado: {backupFileName}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Erro ao criar backup: {e.Message}");
            }
        }
        
        /// <summary>
        /// Remove backups antigos para não ocupar muito espaço
        /// </summary>
        private void CleanupOldBackups()
        {
            try
            {
                string[] backupFiles = Directory.GetFiles(backupDirectory, "backup_*.json");
                
                if (backupFiles.Length > maxBackupFiles)
                {
                    // Ordena por data de criação
                    Array.Sort(backupFiles, (x, y) => File.GetCreationTime(x).CompareTo(File.GetCreationTime(y)));
                    
                    // Remove os mais antigos
                    int filesToRemove = backupFiles.Length - maxBackupFiles;
                    for (int i = 0; i < filesToRemove; i++)
                    {
                        File.Delete(backupFiles[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Erro ao limpar backups: {e.Message}");
            }
        }
        
        /// <summary>
        /// Tenta restaurar dados do backup mais recente
        /// </summary>
        private bool TryRestoreFromBackup()
        {
            try
            {
                if (!Directory.Exists(backupDirectory)) return false;
                
                string[] backupFiles = Directory.GetFiles(backupDirectory, "backup_*.json");
                if (backupFiles.Length == 0) return false;
                
                // Pega backup mais recente
                Array.Sort(backupFiles, (x, y) => File.GetCreationTime(y).CompareTo(File.GetCreationTime(x)));
                string latestBackup = backupFiles[0];
                
                // Tenta carregar backup
                string backupData = File.ReadAllText(latestBackup);
                
                if (enableEncryption)
                {
                    backupData = SimpleDecrypt(backupData);
                }
                
                SaveData restoredData = JsonUtility.FromJson<SaveData>(backupData);
                
                if (ValidateLoadedData(restoredData))
                {
                    currentSaveData = restoredData;
                    SaveData(currentSaveData); // Salva dados restaurados
                    
                    Debug.Log($"[SaveSystem] Dados restaurados do backup: {Path.GetFileName(latestBackup)}");
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Erro ao restaurar backup: {e.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Salvamento automático
        /// </summary>
        private void AutoSave()
        {
            if (hasUnsavedChanges && currentSaveData != null)
            {
                SaveData();
                
                if (enableDebugLogs)
                    Debug.Log("[SaveSystem] Auto-save executado");
            }
        }
        
        /// <summary>
        /// Marca que há mudanças não salvas
        /// </summary>
        public void MarkDataDirty()
        {
            hasUnsavedChanges = true;
        }
        
        /// <summary>
        /// Atualiza tempo de jogo atual
        /// </summary>
        public void UpdatePlayTime(float sessionTime)
        {
            if (currentSaveData != null)
            {
                currentSaveData.totalPlayTime += sessionTime;
                
                if (sessionTime > currentSaveData.longestPlaySession)
                {
                    currentSaveData.longestPlaySession = sessionTime;
                }
                
                MarkDataDirty();
            }
        }
        
        /// <summary>
        /// Criptografia simples para proteger dados básicos
        /// </summary>
        private string SimpleEncrypt(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)(chars[i] ^ encryptionKey[i % encryptionKey.Length]);
            }
            
            return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(chars));
        }
        
        /// <summary>
        /// Descriptografia simples
        /// </summary>
        private string SimpleDecrypt(string encodedText)
        {
            if (string.IsNullOrEmpty(encodedText)) return encodedText;
            
            try
            {
                byte[] bytes = System.Convert.FromBase64String(encodedText);
                char[] chars = System.Text.Encoding.UTF8.GetChars(bytes);
                
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i] = (char)(chars[i] ^ encryptionKey[i % encryptionKey.Length]);
                }
                
                return new string(chars);
            }
            catch
            {
                return encodedText; // Retorna original se falhar
            }
        }
        
        // Métodos públicos para acesso aos dados
        
        public SaveData GetCurrentSaveData() => currentSaveData;
        public bool HasSaveFile() => File.Exists(saveFilePath);
        public bool HasUnsavedChanges() => hasUnsavedChanges;
        public float GetTimeSinceLastSave() => Time.time - lastSaveTime;
        
        /// <summary>
        /// Reseta todos os dados (para debug ou novo jogo)
        /// </summary>
        public void ResetAllData()
        {
            currentSaveData = new SaveData();
            SaveData(currentSaveData);
            
            if (enableDebugLogs)
                Debug.Log("[SaveSystem] Todos os dados foram resetados");
        }
        
        /// <summary>
        /// Exporta dados para debug ou transferência
        /// </summary>
        public string ExportDataAsJson()
        {
            if (currentSaveData == null) return "";
            
            return JsonUtility.ToJson(currentSaveData, true);
        }
        
        /// <summary>
        /// Importa dados de string JSON
        /// </summary>
        public bool ImportDataFromJson(string jsonData)
        {
            try
            {
                SaveData importedData = JsonUtility.FromJson<SaveData>(jsonData);
                
                if (ValidateLoadedData(importedData))
                {
                    currentSaveData = importedData;
                    SaveData(currentSaveData);
                    
                    OnDataLoaded?.Invoke(currentSaveData);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Erro ao importar dados: {e.Message}");
            }
            
            return false;
        }
        
        void OnApplicationPause(bool pauseStatus)
        {
            // Salva quando o app é pausado (mobile)
            if (pauseStatus && hasUnsavedChanges)
            {
                SaveData();
            }
        }
        
        void OnApplicationFocus(bool hasFocus)
        {
            // Salva quando perde foco (desktop)
            if (!hasFocus && hasUnsavedChanges)
            {
                SaveData();
            }
        }
        
        void OnDestroy()
        {
            // Salva final ao destruir objeto
            if (hasUnsavedChanges && currentSaveData != null)
            {
                SaveData();
            }
        }
        
        void OnGUI()
        {
            if (showSaveInfoInGUI && currentSaveData != null)
            {
                GUI.Box(new Rect(10, 140, 200, 100), "Sistema de Save");
                GUI.Label(new Rect(20, 160, 180, 20), $"Último save: {lastSaveTime:F1}s");
                GUI.Label(new Rect(20, 180, 180, 20), $"Mudanças: {hasUnsavedChanges}");
                GUI.Label(new Rect(20, 200, 180, 20), $"Tempo total: {currentSaveData.totalPlayTime:F1}h");
                GUI.Label(new Rect(20, 220, 180, 20), $"Dias jogados: {currentSaveData.daysPlayed}");
            }
        }
    }
}