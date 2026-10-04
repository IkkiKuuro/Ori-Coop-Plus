# Ferramentas e Scripts de Desenvolvimento e Engenharia Reversa

Este diretorio reune todos os scripts, ferramentas de inspecao e artefatos de engenharia reversa criados durante a analise de assemblies e desenvolvimento do mod **Ori Coop Plus**.

---

## 1. Reparacao e Diagnostico da Instalacao do Jogo

- **[`clean_unityengine_cecil.ps1`](clean_unityengine_cecil.ps1)**:  
  Utiliza a biblioteca `Mono.Cecil` para purificar o arquivo `UnityEngine.dll` (`oriDE_Data\Managed\UnityEngine.dll`) caso ele tenha sido adulterado por mod loaders legados (injecao de chamada para `WWClient.MainLoader::Load` em `MonoBehaviour.Awake`). Remove metodos e referencias indevidas restaurando a integridade original exigida pelo Unity 5.3.2f1 e BepInEx.
- **[`check_unity_logs.ps1`](check_unity_logs.ps1)**:  
  Verifica e exibe as ultimas 60 linhas dos logs do Unity (`output_log.txt`) nos tres caminhos possiveis: raiz do jogo no Steam, pasta `oriDE_Data` e `AppData\LocalLow\Moon Studios\Ori and the Blind Forest Definitive Edition`.
- **[`check_loader_exceptions.ps1`](check_loader_exceptions.ps1)**:  
  Analisa erros de carregamento e `ReflectionTypeLoadException` em assemblies do jogo.

---

## 2. Inspecao de Assemblies e Engenharia Reversa (Reflection e Cecil)

- **[`disasm.ps1`](disasm.ps1)**:  
  Desmonta metodos em IL (Intermediate Language) diretamente de `Assembly-CSharp.dll` ou plugins para inspecao profunda de instrucoes.
- **[`dump_all_members.ps1`](dump_all_members.ps1)**:  
  Gera dump completo com assinaturas de metodos, propriedades e campos dos tipos mapeados do jogo.
- **[`dump_complete.ps1`](dump_complete.ps1)**:  
  Executa varredura profunda de reflection em todos os modulos carregados.
- **[`dump_target_types.ps1`](dump_target_types.ps1)**:  
  Extrai membros e estruturas de tipos-chave do Ori (`SeinCharacter`, `InventoryManager`, `CleverMenuItemSelectionManager`, `MenuScreenManager`, `CleverMenuItem`, etc.).
- **[`dump_preload.ps1`](dump_preload.ps1)**:  
  Inspeciona as classes do fluxo de boot do Unity (`LoadingBootstrap`, `GameController`, etc.).
- **[`dump_resolved.ps1`](dump_resolved.ps1)** / **[`test_resolve.ps1`](test_resolve.ps1)**:  
  Testa resolucao de tipos e dependencias entre os assemblies legados e a versao BepInEx.
- **[`extract_strings.ps1`](extract_strings.ps1)**:  
  Varre strings literais embutidas em binarios para descoberta de nomes de menus, botoes e notificacoes de interface.

---

## 3. Inspecao de Sistemas Especificos do Jogo

- **[`find_characters.ps1`](find_characters.ps1)** / **[`inspect_game_characters.ps1`](inspect_game_characters.ps1)** / **[`inspect_game_characters2.ps1`](inspect_game_characters2.ps1)**:  
  Investigam as referencias globais de personagens (`Game.Characters.Sein`).
- **[`inspect_sein.ps1`](inspect_sein.ps1)** / **[`inspect_sein2.ps1`](inspect_sein2.ps1)** / **[`inspect_sein_fields.ps1`](inspect_sein_fields.ps1)**:  
  Mapeiam os campos privados e controladores do Sein (`SeinController`, `SeinInventory`, habilidades).
- **[`inspect_cas.ps1`](inspect_cas.ps1)** / **[`inspect_cas_methods.ps1`](inspect_cas_methods.ps1)**:  
  Inspecionam a maquina de animacao `CleverAnimationSet` e reproducao de estados do puppet.
- **[`inspect_clone.ps1`](inspect_clone.ps1)**:  
  Testa e investiga a viabilidade de clonagem de prefabs para puppet multiplayer.
- **[`inspect_frustum.ps1`](inspect_frustum.ps1)** / **[`inspect_cfo.ps1`](inspect_cfo.ps1)** / **[`search_vis_methods.ps1`](search_vis_methods.ps1)**:  
  Investigam culling de camera e tecnicas de forcar visibilidade de puppets remotos fora do frustum.
- **[`inspect_ta.ps1`](inspect_ta.ps1)**:  
  Inspeciona animadores de transformacao (`TransformAnimator`).
- **[`inspect_ifo_impls.ps1`](inspect_ifo_impls.ps1)**:  
  Inspeciona interfaces e implementacoes de entidades interativas.
- **[`inspect_wwclient.ps1`](inspect_wwclient.ps1)**:  
  Examina o binario legado `WWClient.dll` para extracao e migracao da logica de rede.
- **[`inspect_dll.ps1`](inspect_dll.ps1)** / **[`inspect_methods.ps1`](inspect_methods.ps1)**:  
  Utilitarios genericos para leitura rapida de metadados de DLLs.

---

## 4. Ambiente, Download e Compilacao

- **[`get_bepinex.ps1`](get_bepinex.ps1)** / **[`extract_bepinex.ps1`](extract_bepinex.ps1)** / **[`extract_bepinex_all.ps1`](extract_bepinex_all.ps1)** / **[`list_zip_dlls.ps1`](list_zip_dlls.ps1)**:  
  Baixam e descompactam a versao correta do BepInEx 5.4.x para x86 (32-bit).
- **[`download_dotnet8.ps1`](download_dotnet8.ps1)**:  
  Faz o download automatico do SDK do .NET 8 para compilacao do servidor dedicado `OriCoopDedicatedServer`.
- **[`test_build.ps1`](test_build.ps1)** / **[`test_build_rsp.ps1`](test_build_rsp.ps1)**:  
  Testam compilacao com `csc.exe` (.NET Framework 4.0 target 3.5) usando arquivo de resposta (`build.rsp`).
- **[`validate_dll.ps1`](validate_dll.ps1)**:  
  Valida o bitness (32-bit / AnyCPU) e metadados da DLL gerada antes da publicacao.

---

## 5. Arquivos de Dump e Referencia

- `*.txt` (`target_types_dump.txt`, `all_members_dump.txt`, `disasm_out.txt`, `strings_found.txt`, `all_decompiled_types.txt`, `all_types_resolved.txt`):  
  Dumps estaticos gerados durante as analises das DLLs do Ori DE, servindo de documentacao de referencia para estruturas de campos, metodos e offsets.
- `build.rsp`:  
  Arquivo de resposta com flags e referencias de compilacao para o compilador C#.
