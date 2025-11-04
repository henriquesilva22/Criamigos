# 🎮 Criamigos - Pet Virtual com IA

Um jogo educativo e divertido onde crianças podem cuidar de criaturas virtuais que falam, ouvem e evoluem através de inteligência artificial. Desenvolvido em Unity com foco total na segurança infantil.

## ✨ Funcionalidades

- **Sistema de Necessidades**: Fome, energia e felicidade que diminuem com o tempo
- **Interação por Voz**: Speech-to-Text → IA → Text-to-Speech para conversas naturais
- **Animações Reativas**: Criatura reage ao seu estado emocional e interações
- **Interface Intuitiva**: Barras visuais, botões e feedback em tempo real
- **IA Segura para Crianças**: Respostas filtradas e prompts apropriados para idade

## 🚀 Instalação Rápida

### 1. Pré-requisitos

- **Unity 2021.3 LTS** ou superior (recomendado: 2022.3 LTS)
- **Chave da API OpenAI** ([obter aqui](https://platform.openai.com/))
- **Modelo 3D** da criatura (pode usar da Asset Store ou Mixamo)

### 2. Setup do Projeto

1. **Clone o repositório**:
   ```bash
   git clone https://github.com/seu-usuario/criamigos-prototype.git
   cd criamigos-prototype
   ```

2. **Abra no Unity**:
   - Unity Hub → Open → Selecione a pasta `unity-project/`
   - Aguarde a importação dos assets

3. **Configure as APIs**:
   ```bash
   # Copie o arquivo de exemplo
   copy docs\config.example.json unity-project\Assets\StreamingAssets\config.json
   
   # Edite com suas chaves reais
   notepad unity-project\Assets\StreamingAssets\config.json
   ```

4. **Importe um modelo 3D**:
   - Asset Store → Procure por "Cartoon Character" ou similar
   - Ou baixe do Mixamo um personagem rigged
   - Arraste para `Assets/Art/`

### 3. Configuração da Cena

1. **Crie a cena protótipo**:
   - File → New Scene
   - Save como `Assets/Scenes/Prototype.unity`

2. **Adicione a criatura**:
   - Arraste o modelo 3D para a cena
   - Certifique-se de que tem Animator

3. **Configure os scripts**:
   - Adicione `HungerSystem` ao modelo da criatura
   - Adicione `CreatureController` ao mesmo GameObject
   - Adicione outros managers a GameObjects vazios

4. **Crie a UI**:
   - Crie Canvas → UI → Canvas
   - Adicione Sliders para fome/energia/felicidade
   - Adicione Buttons para ações (Feed, Play, Rest, Talk)
   - Configure `UIManager` e conecte as referências

## 🎮 Como Usar

### Controles Básicos

- **Alimentar** 🍎: Clique no botão Feed para aumentar a fome
- **Brincar** 🎾: Clique em Play para aumentar a felicidade  
- **Descansar** 😴: Clique em Rest para aumentar a energia
- **Conversar** 🎙️: Mantenha pressionado Talk, fale, e solte

### Sistema de Conversa

1. **Pressione e segure** o botão Talk
2. **Fale claramente** para o microfone
3. **Solte o botão** quando terminar
4. A criatura irá **processar** sua fala e **responder** com voz

### Estados da Criatura

- 🟢 **Saudável** (70-100%): Criatura feliz e ativa
- 🟡 **Atenção** (30-70%): Precisa de alguns cuidados
- 🔴 **Crítico** (0-30%): Urgente! Cuide imediatamente

## ⚙️ Configuração das APIs

### Arquivo config.json

```json
{
  "openai_api_key": "sk-sua-chave-aqui",
  "ai_model": "gpt-3.5-turbo",
  "tts_voice": "nova",
  "tts_speed": 1.0,
  "ai_temperature": 0.7
}
```

### Obter Chave OpenAI

1. Acesse [platform.openai.com](https://platform.openai.com/)
2. Faça login ou crie uma conta
3. Vá em API Keys → Create new secret key
4. Copie a chave e cole no `config.json`

**⚠️ IMPORTANTE**: Nunca comite o arquivo `config.json` com chaves reais no Git!

## 🛠️ Estrutura dos Scripts

### Scripts Principais

- **`HungerSystem.cs`** - Gerencia fome, energia e felicidade
- **`CreatureController.cs`** - Controla animações e comportamentos
- **`SpeechToTextManager.cs`** - Converte voz em texto
- **`AIResponder.cs`** - Gera respostas inteligentes e seguras
- **`TextToSpeechManager.cs`** - Converte texto em voz
- **`UIManager.cs`** - Interface e feedback visual

### Dependências Unity

Todos os scripts usam apenas **APIs padrão do Unity**:
- UnityWebRequest (HTTP)
- Microphone (Gravação)
- AudioSource (Reprodução)
- UI System (Interface)

## 📱 Build para Mobile

### Android

1. **Configurações de Build**:
   ```
   File → Build Settings → Android
   Switch Platform → Player Settings
   ```

2. **Permissões necessárias**:
   - Microphone ✅
   - Internet ✅

3. **Build**:
   ```
   Build Settings → Build
   Escolha pasta de destino → Build
   ```

### iOS

1. **Requisitos**:
   - Xcode instalado
   - Certificado de desenvolvedor Apple

2. **Configurações**:
   - Target SDK: iOS 12.0+
   - Microphone Usage Description: "Para conversar com seu Criamigo"

## 🔧 Troubleshooting

### Problemas Comuns

**❌ Erro: "API Key not found"**
```
✅ Solução: Verifique se config.json está em Assets/StreamingAssets/
```

**❌ Microfone não funciona**
```
✅ Solução: 
- Permita acesso ao microfone no Windows/macOS
- No Unity: Edit → Project Settings → XR Plug-in Management
```

**❌ TTS não reproduz áudio**
```
✅ Solução:
- Verifique se AudioSource está configurado
- Volume do dispositivo não está mudo
```

**❌ IA responde em inglês**
```
✅ Solução:
- No AIResponder, configure systemPrompt em português
- Ajuste language: "pt" no SpeechToTextManager
```

### Logs de Debug

Para ativar logs detalhados, marque **"Enable Debug Logs"** nos scripts:
- SpeechToTextManager
- AIResponder  
- TextToSpeechManager

## 🚨 Segurança & Produção

### Para Desenvolvimento
- ✅ Use chaves de API diretamente no config.json
- ✅ Adicione config.json ao .gitignore

### Para Produção
- ❌ NUNCA coloque chaves no cliente final
- ✅ Crie um proxy/backend server que segura as chaves
- ✅ Cliente se comunica apenas com seu servidor
- ✅ Servidor faz chamadas para OpenAI

### Exemplo de Proxy Simples
```javascript
// server.js (Node.js + Express)
app.post('/api/chat', async (req, res) => {
  const response = await openai.chat.completions.create({
    model: 'gpt-3.5-turbo',
    messages: req.body.messages
  });
  res.json(response);
});
```

## 📚 Recursos Adicionais

### Modelos 3D Gratuitos
- [Mixamo](https://mixamo.com) - Personagens rigged gratuitos
- [Unity Asset Store](https://assetstore.unity.com) - Busque "Free Character"

### Efeitos Sonoros
- [Freesound](https://freesound.org) - Sons gratuitos
- [Unity Audio](https://unity.com/products/unity-audio) - Ferramentas de áudio

### Tutoriais Unity
- [Unity Learn](https://learn.unity.com) - Tutorials oficiais
- [Brackeys](https://youtube.com/brackeys) - YouTube tutorials

## 🤝 Contribuindo

1. Fork o repositório
2. Crie uma branch: `git checkout -b feature-nova`
3. Commit as mudanças: `git commit -m "Adiciona feature nova"`
4. Push: `git push origin feature-nova`
5. Abra um Pull Request

## 📄 Licença

MIT License - Veja [LICENSE](LICENSE) para detalhes.

## 🆘 Suporte

- **Issues**: [GitHub Issues](https://github.com/seu-usuario/criamigos-prototype/issues)
- **Discussões**: [GitHub Discussions](https://github.com/seu-usuario/criamigos-prototype/discussions)
- **Email**: seu-email@exemplo.com

---

**Feito com ❤️ para crianças aprenderem e se divertirem!** 🎮✨