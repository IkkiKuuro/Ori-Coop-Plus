# Context (ingest synthesis)

## Topic: arquitetura geral do mod (cliente BepInEx + servidor dedicado)
- source: docs/architecture.md
- notes: Tres partes — plugin BepInEx (RemotePlayerManager, puppets, RemoteVisualController, AnimationRegistry, Harmony + NetworkService + Patches), OriCoopShared (contratos), servidor dedicado novo core (Net/Transport, Session, Game, Diagnostics, sem dependencias externas). Core antigo OriCoopDedicatedServer.Core removido em 2026-10-05; sem dual-stack nem fallback. Projetos: OriCoopBepInEx (.NET Framework 3.5), OriCoopShared (compartilhado), OriCoopDedicatedServer (.NET 8). Ciclos de inicializacao do cliente (Awake, Harmony, snapshots por FixedUpdate) e do servidor (ServerBoot, NetServerHost, loop de console).

## Topic: arquitetura do cliente BepInEx
- source: docs/bepinex-architecture.md
- notes: Plugin BepInEx 5.x para Ori DE 32-bit (net35). Camadas: Plugin/OriCoopPlugin; Client (RemotePlayerManager, RemotePuppetFactory com puppet leve so da subarvore visual, RemotePlayerPuppet com interpolacao e FloatingNameTag, RemoteVisualController watchdog, AnimationRegistry); UI (InventoryScreenPatch, OriCoopMenuScreen, NativeUIHelper); Diagnostics (ReplicationObservability); Domain (DTOs); Networking/NetworkService; Patches (SeinCharacterPatch, SeinInputPatch, FrustumCullingBypassPatch, AnimationPrewarmPatch). Restricoes C# 5 (sem $̈"", sem ?., Action com no maximo 4 parametros), entrypoint LoadingBootstrap, dependencias de Assembly-CSharp.

## Topic: UI nativa e menu Ori Coop
- source: docs/native-ui-architecture.md
- notes: UI do Ori DE usa hierarquia propria de GameObjects (TextBox/MessageBox, CleverMenuItem, CleverMenuItemSelectionManager, CleverMenuItemLayout) — sem UGUI nem OnGUI. Dois sistemas de pausa: InventoryManager (no save; recebe botao ORI COOP injetado) vs PauseScreen (cutscenes; sem injecao). Submenu OriCoopMenuScreen clona elementos nativos com compatibilidade gamepad + teclado, preservando a pausa.

## Topic: contexto do jogo e do mod
- source: docs/game-and-mod.md
- notes: Alvo = Ori and the Blind Forest: Definitive Edition (Windows/Steam, Unity). Multiplayer injeta modulo no cliente + servidor dedicado distribui estado; teste dentro de save com Ori controlavel (menu/prologo com Naru fora do fluxo). Vocabulario (jogador local/remoto, orbe seguidor, habilidade, pickup, porta/alavanca, world event, breakable, entity sync). Recursos cooperativos via opcoes do servidor (teleporte, habilidades, story only, world events, doors and levers, nomes, client colors, entity sync — oitavo bool do CONFIG_SYNC 16). Config local com.ikkikuuro.oricoop.cfg (Host/Port/PlayerId/Nickname); MPSettings.json legado fora de uso. Auditoria vanilla 2026-10-10 confirma MoonGuid, PickupBase, IAttackable, SeinStomp, SeinSpiritFlameAbility; sem Boss/SpiritWell/Breakable no vanilla.

## Topic: mapa de entradas e entidades do jogo
- source: docs/game-entry-map.md
- notes: Cobertura de sync por entrada com legenda Implementado/Reconhecido/A confirmar/Nao implementado. Auditoria 2026-10-10: src atual so tem 6 patches e recepcao cliente de SKILL/SYNC_* valida e descarta sem aplicar — secoes Implementado valem como relay sem aplicacao ate o handler voltar. Enum AbilityType com 44 valores, SeinCharacter (17 campos), flags do SeinController, PickupBase + SeinPickupProcessor (8 OnCollect*), Lever/DoorWithSlots/EnergyDoor, SetWorldEventAction + WorldEventsRuntime + WorldEventsManager + MoonGuid, inimigos sem classe Boss (sequencias Kuro*/Ginso/Forlorn/Horu), save/teleporte = SavePedestal + SoulFlame/Checkpoint + SaveGameController.

## Topic: operacao, build e diagnostico
- source: docs/operations.md
- notes: Pre-requisitos (Windows, Ori DE Steam, .NET SDK, caminho oriDE_Data\Managed). Build: dotnet build da solution + csproj do servidor; cliente tambem via build.ps1 (localiza oriDE_Data\Managed, usa BepInEx.dll e 0Harmony.dll) ou dotnet CLI. Fallback historico sem SDK (Roslyn + ref assemblies via csc.dll). Legado OriCoopDedicatedServer.Core excluido em 2026-10-05. Deploy exige jogo/servidor fechados (<ORI_DIR>\BepInEx\plugins\, <ORI_DIR>\Server\).

## Topic: mapa do codigo
- source: docs/code-map.md
- notes: Cliente (OriCoopPlugin, Client/, Diagnostics/, Domain/, NetworkService, Patches/, OriCoopShared/) e servidor novo core (Program.cs/ServerBoot, Net/Transport EnvelopeCodec+UdpTransport, Net/Session SessionManager, Net/Reliability AckTracker, Net/Game ServerBoot/GameHandlers/ConfigStore/DummyBot/PlayerStateRelay, Net/Game/Commands, Net/Diagnostics FileConsoleLogger, SmokeProbe com --test all). Legado removido em 2026-10-05: nao recriar Server.cs/Client.cs/Packet.cs. Fontes de catalogo: AllGames.txt, AllMods.txt, WWGames.txt, CHANGELOGS/.

## Topic: fase anim-sync (contexto de planejamento, proposto)
- source: docs/anim-sync-CONTEXT.md
- notes: Status Ready for planning (nao bloqueado). Escopo: retrabalhar sync de anims de movimentacao do puppet (sem loop parado, sem sprite aleatorio). Decisoes de fase D-01..D-20 (inventario com dump duplo, enum ActionVisualState de 12 estados como autoridade, sender com Speed/grounded/FacingLeft reais, envio on-change + heartbeat 2-5 Hz, pacote novo com ID novo, servidor so repassa, sem compat retroativa, fail-closed, diagnostico por transicao). Implementado na sequencia: s_nameToState explicito + anim-catalog.md + PLAYER_STATE 18.

## Topic: catalogo de animacoes de movimentacao
- source: docs/anim-catalog.md
- notes: Mapeamento nome → ActionVisualState para AnimationRegistry.s_nameToState; aliases SEED best-effort, nomes exatos a confirmar via dump em jogo (F8, linhas [ANIM-DUMP]). Ordem de resolve: exato → estado-Sein → manter ultima (miss nunca vira sprite aleatorio). Fallback por estado so aceita clipes do Sein (CollectClips via reflection); scan global so alimenta resolve exato. Sender deriva estado com prioridade controller → nome do clipe → velocidade; IsOnGround real.

## Topic: fase player-event-core (contexto de planejamento, proposto)
- source: docs/player-event-core-CONTEXT.md
- notes: Status Ready for planning (nao bloqueado). Escopo fatiado: entrega = infra PlayerEventCore + 1 evento piloto (Spirit Flame); demais categorias (Stomp, Bash, ChargeJump, Dash, Glide, VFX/SFX genericos, anims gerais, morte/respawn, dano/vida, entidades) sao fases futuras. Decisoes de fase D-01..D-15: core central + bus/registro, deteccao via Harmony (nao polling), so jogador local publica, piloto unreliable sequenciado com ID novo (PLAYER_EVENT 19), payload minimo (direcao + posicao de origem + timestamp), sem throttle no piloto, fidelidade visual sem efeito gameplay (projetil fake agora, fisica real depois), fail-closed em evento desconhecido. Classe/metodo vanilla do Spirit Flame a confirmar via dnSpy/ILSpy.

## Topic: investigacao de animacoes, inimigos, dano e save
- source: docs/animation-enemies-damage-data.md
- notes: Investigacao estatica de 2026-10-13 sobre install em D:, sem execucao do jogo; VFX a confirmar em run com 2 clientes. Cadeia de animacao vanilla (SeinCharacter.Animation → CharacterAnimationSystem → SpriteAnimatorWithTransitions → TextureAnimationWithTransitions → TextureAnimation) e drivers logicos por estado + CharacterSpriteMirror. Cobertura do mod hoje (sender PlayerStateReader, catalogo AnimationRegistry, 6 patches) e lacunas (inimigos, dano, save/progresso) com leitura dupla de mecanicas de dados: dano (DamageType, DamageDealer/Receiver, vida/energia) e save (SaveGameController, playerData.txt, inventario).

## Topic: guia do mod e instalacao (README raiz)
- source: README.md
- notes: Ori Coop Plus adiciona multiplayer cooperativo ao Ori DE: modulo cliente BepInEx 5.x (.NET Framework 3.5 estrito) + servidor dedicado; pastas de instalacao separadas. Aponta para docs/ como documentacao tecnica e para docs/bepinex-architecture.md antes de integrar pacotes/patches. (Nota: titulo H1 WW Launcher e legado do launcher; corpo descreve o Ori Coop Plus.)

## Topic: regras de manutencao do repositorio
- source: AGENTS.md
- notes: docs/ deve ser atualizada em toda mudanca relevante (nova pagina quando assunto nao coberto, marcar a confirmar o nao confirmado, atualizar indice docs/README.md, registrar testes em docs/operations.md). Deploy obrigatorio pos-build: substituir DLL/exe na instalacao do Ori DE (BepInEx\plugins\, Server\) com jogo fechado; caminhos Steam conhecidos. Graphify obrigatorio (install, build inicial se STALE, query antes de perguntas de arquitetura).

## Topic: scripts de desenvolvimento e engenharia reversa
- source: scripts/README.md
- notes: Catalogo de scripts PowerShell: reparo/diagnostico da instalacao (clean_unityengine_cecil, check_unity_logs, check_loader_exceptions), inspecao de assemblies (disasm, dump_all_members, dump_complete, dump_target_types, dump_preload, dump_resolved/test_resolve, extract_strings, find/inspect characters, inspect_sein*, inspect_cas*, test_build*, validate_dll, get/extract_bepinex, list_zip_dlls, download_dotnet8).

## Topic: atribuicao de codigo de terceiros
- source: Modules/HKModules/Readme.md
- notes: Codigo parcialmente originado do mod HKMP (https://github.com/Extremelyd1/HKMP). Sinais finos; sem marcadores ADR/PRD/SPEC.
