# CameraMan para Windows

**[Português](README.md) | [English](README.en.md)**

Uma câmera flutuante para o Windows 11 — coloque você mesmo na tela em gravações, demonstrações, aulas e chamadas.

Este repositório é um **fork** do [CameraMan para macOS](https://github.com/lucianodiisouza/camera-man-macos) e contém o
port nativo para Windows: o mesmo produto, o mesmo comportamento e a mesma licença MIT, reimplementado em C# com WPF e
a pilha de câmera do Windows. O app para macOS continua sendo o projeto upstream e não é modificado aqui.


- Original para macOS: <https://github.com/lucianodiisouza/camera-man-macos>
- Download: [CameraMan-Windows-1.0.0-win-x64.zip](https://github.com/httpjeffersom/camera-man-windows/releases/download/v1.0.0/CameraMan-Windows-1.0.0-win-x64.zip)

> **Esta é uma versão inicial do port.** O app funciona, mas ainda podem existir erros de interface, ícones errados ou
> faltando e outros detalhes a ajustar; eles serão corrigidos aos poucos. Veja
> [Situação: o que ainda falta](#situação-o-que-ainda-falta).

## Recursos

- **Feito para gravar**: os atalhos globais funcionam de qualquer app (`Ctrl+Alt+C` mostra/oculta, `Ctrl+Alt+S` troca o
  tamanho); a câmera flutuante fica acima das outras janelas; a captura para completamente enquanto a câmera está oculta
  ou a sessão está bloqueada.
- **Formatos**: círculo, orgânico (cinco predefinições, embaralhar, respiração opcional), quadrado, vertical 9:16 e
  horizontal 16:9, com borda suave e raio de canto por formato.
- **Enquadramento**: mover a imagem dentro do formato, zoom, espelhar ou inverter.
- **Aparência**: brilho, contraste e saturação nos quadros da câmera; borda sólida ou em degradê; sombra suave.
- **Janela**: cinco tamanhos, encaixe em qualquer canto, cada tamanho lembra sua própria posição e monitor, `Espaço`
  alterna entre seus dois tamanhos favoritos.
- **Janela de Configurações** no estilo do Windows, com nove páginas e busca.
- **Ícone na área de notificação**, iniciar com o Windows opcional, inglês e português do Brasil.
- **Privado**: sem acesso à rede, sem conta, sem analytics — o vídeo nunca sai da máquina.

## Notas do port

| macOS | Port para Windows |
|---|---|
| Swift, SwiftUI, AVFoundation | C#, WPF, `MediaCapture` do WinRT |
| ⌃⌥⌘C / ⌃⌥⌘S | `Ctrl+Alt+C` / `Ctrl+Alt+S` (o Windows reserva a maioria das combinações com a tecla Windows) |
| Core Image na GPU | Tabela de consulta na CPU + saturação em ponto fixo |
| Borda suave borrada para fora, janela com preenchimento | Borda suave esmaecida para dentro a partir do contorno (sem preenchimento transparente) |
| Ícone na barra de menus, ícone opcional no Dock | Ícone na área de notificação, sem botão na barra de tarefas |
| Estilo de Ajustes do macOS | Estilo de Configurações do Windows 11, barra de título nativa |
| `UserDefaults` | `%APPDATA%\CameraMan\settings.json` |
| App Sandbox + notarização | Executável comum por usuário, ainda sem instalador |

A análise completa está em [docs/PORTING_PLAN.md](docs/PORTING_PLAN.md), o mapeamento API por API em
[docs/WINDOWS_APIS.md](docs/WINDOWS_APIS.md) e a matriz de verificação em
[docs/FEATURE_STATUS.md](docs/FEATURE_STATUS.md).

## Requisitos

- Windows 11 (x64). O Windows 10 2004 ou superior deve funcionar, mas não é o alvo testado.
- Uma webcam. O app usa a pilha de câmera padrão do Windows e pede acesso ao sistema no primeiro uso.
- Para compilar: .NET SDK 10.0.401 ou superior. Veja [docs/BUILDING.md](docs/BUILDING.md).
- Para executar uma cópia publicada: nada mais. As builds self-contained incluem o runtime do .NET.

## Começando

Baixe o zip na [página de releases](https://github.com/httpjeffersom/camera-man-windows/releases/tag/v1.0.0), descompacte
em qualquer pasta e execute `CameraMan.Windows.exe`. Não há instalador nem necessidade de direitos de administrador;
como o executável não é assinado, o SmartScreen pode avisar na primeira execução ("Mais informações" → "Executar
assim mesmo").

A partir do código:

```powershell
dotnet build CameraMan.Windows.slnx -c Release
dotnet run --project src/CameraMan.Windows -c Release
```

Ou execute a build publicada e self-contained:

```powershell
.\publish\win-x64\CameraMan.Windows.exe
```

Na primeira execução a janela da câmera aparece no canto superior esquerdo do monitor principal, com 400 px de largura.
O ícone na bandeja é o caminho de volta para o app depois que você fecha a janela da câmera.

## Teclado

| Atalho | Ação |
|---|---|
| `Ctrl+Alt+C` | Mostra/oculta a câmera (global; pode ser desativado nas Configurações) |
| `Ctrl+Alt+S` | Passa para o próximo tamanho (global) |
| `Ctrl+,` | Abre as Configurações (com a janela da câmera em foco, ou pelo menu da bandeja) |
| `Ctrl+Q` | Sai (janela da câmera ou menu da bandeja) |
| Setas | Movem o enquadramento |
| `+` / `-` / `R` | Aproxima, afasta, restaura o zoom |
| `/` / `V` | Espelha na horizontal / na vertical |
| `O` | Próximo formato |
| `Backspace` | Próxima câmera |
| `Espaço` | Alterna entre os dois tamanhos favoritos |
| `Ctrl+F` | Busca nas Configurações |
| `Esc` | Fecha as Configurações (quando a caixa de busca está vazia) |

## Onde as coisas ficam

- Configurações: `%APPDATA%\CameraMan\settings.json`
- Log de erros (só é escrito em falhas inesperadas, rotacionado em 256 KB): `%APPDATA%\CameraMan\error.log`
- Iniciar com o Windows: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valor `CameraMan`, escrito apenas quando
  você ativa a opção.

Os quadros da câmera existem apenas na memória. Nunca são gravados em disco nem enviados pela rede, e o projeto não
contém código de rede ou de codificação de mídia.

## Situação: o que ainda falta

O port compila sem avisos e a suíte automatizada passa, mas ele **não está pronto**. Lacunas e pendências conhecidas:

- **Interface e ícones.** Algumas telas, espaçamentos e ícones ainda diferem do app para macOS ou do próprio Windows 11,
  e alguns textos podem soar estranhos. Isso será corrigido aos poucos.
- **A verificação manual está incompleta.** Prévia ao vivo, formatos, formas, bordas, sombras, borda suave, bandeja,
  atalhos globais e persistência das configurações foram exercitados em hardware real. Multi-monitor com DPI misto,
  retomada após bloqueio/suspensão, restauração da posição por predefinição entre reinícios, troca de câmera com
  `Backspace`, zoom/pan/espelhamento pelo teclado e "restaurar padrões" ainda precisam de um teste manual. A matriz em
  [docs/FEATURE_STATUS.md](docs/FEATURE_STATUS.md) marca cada item como *Verified*, *Implemented* ou *Adapted*.
- **A busca encontra as configurações, mas não rola até elas.** Clicar em um resultado abre a página; a linha
  correspondente não é destacada.
- **Sem instalador e sem assinatura de código.** A pasta publicada é portátil e não assinada, então o SmartScreen avisa
  na primeira execução.
- **O topmost tem limites.** Uma janela topmost normal não consegue cobrir apps DirectX em tela cheia exclusiva; tela
  cheia sem bordas funciona.
- **Os efeitos de vídeo do macOS não existem.** Retrato, Luz de Estúdio e substituição de fundo são recursos do sistema
  sem equivalente público estável no Windows; o app os explica, mas não os simula.
- **A borda suave esmaece só para dentro**, então a silhueta visível fica um pouco mais estreita que no macOS (que borra
  para fora, em preenchimento transparente).
- **A correção de cor roda na CPU**, o que custa mais que o Core Image, mas mantém o app sem dependências.
- **Mudar o idioma exige reiniciar o app.**
- **Não há controle separado de opacidade da sombra**; a cor da sombra mantém seu alfa (o padrão é 50 % de preto).
- **Notificações e atualizações**: não há verificação de atualização nem notificação fora da sobreposição do próprio app.

## Privacidade e segurança

O CameraMan não tem nenhum código de rede: sem cliente HTTP, sem sockets, sem telemetria, sem contas. O fluxo da câmera
é lido quadro a quadro em um buffer reutilizável e desenhado; nunca é codificado ou salvo. Os únicos arquivos tocados são
as configurações/log em `%APPDATA%` e a chave Run do usuário quando você ativa iniciar com o Windows. O app roda como o
usuário atual (`asInvoker`) e nunca pede direitos de administrador. Veja [PRIVACY.md](PRIVACY.md) e
[SECURITY.md](SECURITY.md).

## Compilar do código

Requisitos: Windows 11 x64, .NET SDK 10 e uma câmera para o teste manual.

```powershell
dotnet build CameraMan.Windows.slnx -c Release
dotnet test CameraMan.Windows.slnx -c Release
dotnet publish src\CameraMan.Windows\CameraMan.Windows.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
```

Não é necessário workload do Visual Studio, Windows App SDK, DirectX SDK ou Node.js. Veja
[docs/BUILDING.md](docs/BUILDING.md) para pré-requisitos, o checklist de teste manual e solução de problemas.

## Idiomas

Inglês e português do Brasil. O seletor de idioma fica em Configurações → Geral; a escolha vale na próxima execução.
"Sistema" segue o idioma da interface do Windows instalado.

## Contribuindo

Contribuições são bem-vindas. Veja [CONTRIBUTING.md](CONTRIBUTING.md) para o ambiente de desenvolvimento e
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) para as regras da comunidade. Problemas de segurança: [SECURITY.md](SECURITY.md).

## Histórico de versões

Veja [CHANGELOG.md](CHANGELOG.md).

## Licença

MIT. O projeto original para macOS é Copyright (c) 2025 Camera-Man Contributors; este port acrescenta
Copyright (c) 2026 CameraMan.Windows contributors. Veja [LICENSE](LICENSE) e
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
