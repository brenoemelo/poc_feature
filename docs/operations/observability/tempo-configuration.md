# Melhorias para Produção: Grafana Tempo

Sua configuração atual é um excelente ponto de partida para desenvolvimento e testes locais (modo Single Binary), mas apresenta alguns valores que causariam gargalos severos ou perda prematura de dados em um ambiente de produção.

Para 2025/2026, com o lançamento do Tempo 2.10, as boas práticas evoluíram, especialmente em relação ao formato de armazenamento e gerenciamento de blocos.

Abaixo, apresento a análise detalhada e as otimizações recomendadas:

## 1. Pontos de Atenção e Correções

- **max_block_bytes: 1_000_000 (1MB)**: Este valor está muito baixo. Em produção, blocos pequenos geram fragmentação excessiva, sobrecarregam o compactador e degradam drasticamente a performance de busca. A recomendação padrão é entre 512MB e 1GB.

- **block_retention: 1h**: Seus rastros (traces) serão deletados permanentemente após apenas 1 hora. Para observabilidade real, o mercado utiliza entre 7 a 14 dias (168h a 336h).

- **log_level: debug**: Útil para resolver o problema inicial de "traces não aparecendo", mas deve ser alterado para info em produção para evitar gargalos de I/O de disco com logs desnecessários.

- **search: max_duration: 0**: Na configuração do Tempo, o valor 0 geralmente desabilita o limite, permitindo buscas em todo o período de retenção. No entanto, buscas sem limite de tempo em grandes volumes de dados podem travar o querier.

## 2. Configuração Otimizada (Boas Práticas 2025/2026)

Esta versão atualiza o formato de armazenamento para vParquet5 (oficialmente pronto para produção no Tempo 2.10), que melhora a velocidade de busca e reduz o consumo de memória.

```yaml
server:
  http_listen_port: 3200
  grpc_listen_port: 9095
  log_level: info

query_frontend:
  search:
    max_duration: 0  # Permite buscar em todo o histórico, mas monitore o CPU
    max_spans_per_span_set: 100 # Evita que uma query traga dados excessivos

distributor:
  receivers:
    otlp:
      protocols:
        http:
          endpoint: "0.0.0.0:4318"
        grpc:
          endpoint: "0.0.0.0:4317"

ingester:
  lifecycler:
    ring:
      replication_factor: 1 # Para HA em produção, use 3
      kvstore:
        store: inmemory
  max_block_duration: 5m
  max_block_bytes: 100_000_000 # Aumentado para 100MB (mínimo recomendado para estabilidade)
  trace_idle_period: 30s # Valor padrão que garante a completude do rastro

compactor:
  compaction:
    block_retention: 168h # Retenção de 7 dias (padrão de mercado)
    compacted_block_retention: 1h

metrics_generator:
  processor:
    service_graphs:
    span_metrics:
    local_blocks:
      filter_server_spans: false
      flush_to_storage: true
      concurrent_blocks: 20
  storage:
    path: /tmp/tempo/generator/wal
  traces_storage:
    path: /tmp/tempo/generator/traces

storage:
  trace:
    backend: local
    block:
      version: vParquet5 # Recomendado para Tempo 2.10+ (melhor performance TraceQL)
    wal:
      path: /tmp/tempo/wal
    local:
      path: /tmp/tempo/blocks
    pool:
      max_workers: 100
      queue_depth: 10000

overrides:
  defaults:
    metrics_generator:
      processors: [service-graphs, span-metrics, local-blocks]
```

## 3. Principais Otimizações Recomendadas

1.  **Upgrade para vParquet5**: O formato vParquet4 (seu atual) é estável, mas o vParquet5 introduz colunas dedicadas para atributos nativos como `span:childCount`, o que torna as queries TraceQL muito mais eficientes.

2.  **Ajuste de Ingestão (Batching)**: Se você notar que os traces demoram muito para aparecer, reduza o `max_block_duration`, mas nunca sacrifique o `max_block_bytes` (mantenha-o acima de 100MB) para não criar milhares de arquivos pequenos no disco.

3.  **Habilite o WAL (Write-Ahead Log)**: Sua configuração já possui o caminho do WAL definido, o que é vital. Ele garante que, se o container do Tempo reiniciar, você não perca os traces que ainda estavam na memória (buffer) antes de serem escritos no disco.

4.  **Tail Sampling (Estratégico)**: Em produção, enviar 100% dos traces consome muito disco. Considere configurar o OpenTelemetry Collector para fazer Tail Sampling, garantindo que você armazene 100% dos erros, mas apenas uma porcentagem (ex: 5%) das requisições de sucesso.

## Status da Implementação (2026-02-25)

- **Configuração Aplicada**: O arquivo `tempo.yaml` foi atualizado com os valores de produção (vParquet5, block_retention 168h, max_block_bytes 100MB) e com o processador `local-blocks` corretamente configurado.
- **Validação**: Testes de integração (`PoC.Observability.Tests`) executados com sucesso, confirmando ingestão e recuperação de traces.
- **Correção Local Blocks**: O processador `local-blocks` foi habilitado com `concurrent_blocks: 20` e caminhos de storage definidos, resolvendo o erro de inicialização anterior.
