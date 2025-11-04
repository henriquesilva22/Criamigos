# 🤝 Contribuindo para o Criamigos

Obrigado pelo interesse em contribuir para o Criamigos! Este projeto é focado em criar experiências seguras e educativas para crianças.

## 🎯 Princípios Fundamentais

### 🔒 Segurança Infantil é PRIORIDADE #1
- **NUNCA** remova ou enfraqueça filtros de segurança
- **SEMPRE** teste funcionalidades offline
- **JAMAIS** adicione coleta de dados pessoais
- **MANTENHA** analytics desabilitado para crianças

### 🧹 Código Limpo
- Siga padrões C# e Unity
- Use namespaces organizados
- Comente código complexo
- Mantenha arquitetura event-driven

## 🚀 Como Contribuir

### 1. Fork e Clone
```bash
# Fork no GitHub, depois:
git clone https://github.com/SEU-USUARIO/criamigos.git
cd criamigos
```

### 2. Configurar Ambiente
- Unity 2021.3 LTS ou superior
- Instalar TextMeshPro
- Configurar API OpenAI (opcional)

### 3. Criar Branch
```bash
git checkout -b feature/nome-da-funcionalidade
# ou
git checkout -b bugfix/nome-do-bug
# ou  
git checkout -b security/melhoria-seguranca
```

### 4. Desenvolver
- Teste funcionalidade offline
- Verifique filtros de segurança
- Adicione comentários detalhados
- Siga padrões do projeto

### 5. Testar
- Teste em modo debug (F1)
- Verifique logs no Console
- Teste sem API keys (modo offline)
- Teste em dispositivo móvel (se aplicável)

### 6. Commit e Push
```bash
git add .
git commit -m "feat: adiciona nova funcionalidade X"
git push origin feature/nome-da-funcionalidade
```

### 7. Pull Request
- Use template de PR
- Descreva mudanças detalhadamente
- Inclua testes realizados
- Mencione questões de segurança

## 📝 Tipos de Contribuição

### 🔒 Melhorias de Segurança
- Filtros de conteúdo mais robustos
- Validações adicionais
- Fallbacks offline melhores
- Criptografia mais forte

### ✨ Novas Funcionalidades
- Novas atividades para criatura
- Cores/visuais adicionais
- Melhorias na UI/UX
- Sistemas de progressão

### 🐛 Correções de Bug
- Crashes ou erros
- Problemas de performance
- Compatibilidade mobile
- Save/load issues

### 📚 Documentação
- README melhorias
- Comentários no código
- Exemplos de uso
- Guias de troubleshooting

## 🎨 Padrões de Código

### Namespaces
```csharp
namespace Criamigos.Core      // Sistemas principais
namespace Criamigos.AI       // IA e comunicação
namespace Criamigos.UI       // Interface
```

### Nomenclatura
```csharp
// Classes: PascalCase
public class CreatureController { }

// Métodos: PascalCase
public void FeedCreature() { }

// Variáveis: camelCase
private bool isGameReady;

// Constantes: UPPER_SNAKE_CASE
private const int MAX_TOKENS = 50;
```

### Comentários
```csharp
/// <summary>
/// Descrição detalhada do método/classe
/// Explique propósito, parâmetros e retorno
/// </summary>
/// <param name="input">Descrição do parâmetro</param>
/// <returns>Descrição do retorno</returns>
public string ProcessInput(string input) { }
```

### Eventos e Delegates
```csharp
// Use eventos para comunicação entre sistemas
public event Action<string> OnResponseReceived;

// Chame eventos de forma segura
OnResponseReceived?.Invoke(response);
```

## 🔍 Checklist de Segurança

Antes de submeter PR, verifique:

- [ ] **Filtros de IA**: Nenhum conteúdo inadequado passa
- [ ] **Modo offline**: Funciona sem internet
- [ ] **Dados pessoais**: Nenhuma coleta ou envio
- [ ] **Analytics**: Mantém desabilitado
- [ ] **Validação**: Inputs são validados
- [ ] **Timeouts**: APIs têm limites baixos
- [ ] **Criptografia**: Saves são protegidos
- [ ] **Logs**: Não vazam informações sensíveis

## 🚫 O Que NÃO Fazer

### ❌ Proibido Absoluto
- Remover filtros de conteúdo
- Adicionar analytics/tracking
- Coletar dados pessoais
- Enfraquecer criptografia
- Aumentar limites de IA sem discussão

### ⚠️ Evitar
- Dependências desnecessárias
- Código sem comentários
- Funções muito longas (>50 linhas)
- Magic numbers sem constantes
- Vazamentos de memória

## 🏗️ Arquitetura do Projeto

### Fluxo de Dados
```
User Input → UIManager → GameManager → Specific System
                                   ↓
Specific System → Events → UIManager → Visual Feedback
```

### Sistemas Principais
- **GameManager**: Coordenador central
- **CreatureController**: Lógica da criatura
- **AIResponder**: IA segura
- **TextToSpeechManager**: TTS/Cache
- **SaveSystem**: Persistência
- **UIManager**: Interface

## 📞 Comunicação

### Issues
- **Bugs**: Use template de bug report
- **Features**: Discuta antes de implementar
- **Segurança**: Marque como prioritário

### Discussões
- Use GitHub Discussions para dúvidas
- Seja respeitoso e construtivo
- Foque na segurança infantil

### Review Process
1. Automated checks (se aplicável)
2. Manual code review
3. Security review
4. Testing verification
5. Documentation check

## 🏆 Reconhecimento

Contribuidores são reconhecidos em:
- README.md (seção Créditos)
- CHANGELOG.md (releases)
- Comentários no código (para grandes contribuições)

## 📜 Código de Conduta

- **Seja respeitoso**: Trate todos com cortesia
- **Seja inclusivo**: Bem-vindos desenvolvedores de todos os níveis  
- **Seja construtivo**: Críticas devem incluir sugestões
- **Priorize crianças**: Segurança infantil acima de tudo

---

**Lembre-se**: Estamos construindo algo especial para crianças. Cada linha de código deve refletir nosso compromisso com a segurança e diversão delas! 🎮✨