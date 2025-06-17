# Distributed System Manager

## Overview
Este projeto implementa um sistema distribuído para ingestão, agregação e visualização de dados em tempo real. Foi desenvolvido com o foco de ser o mais realista possível no timeframe estabelecido para o seu desenvolvimento. Utilizando múltiplos componentes desenvolvidos em C# (.NET) e uma interface gráfica baseada em Electron (Node.js). O sistema é composto por:

- **Servidor**: Responsável por receber dados dos agregadores e armazenar no MongoDB.
- **Agregador**: Recebe dados das estações Wavy, agrega-os e envia ao Servidor.
- **Wavy**: Simula sensores que geram e enviam dados para os agregadores.
- **Shared**: Biblioteca partilhada com modelos, serviços de acesso ao MongoDB e RabbitMQ.
- **ComponentCreator**: Utilitário para criar configurações de componentes no MongoDB.
- **Interface Electron**: Permite gerir e monitorizar os componentes do sistema de forma gráfica.

## Estrutura do Projeto
```
├── Agregador/           # Código do Agregador (C#)
├── Servidor/            # Código do Servidor (C#)
├── Wavy/                # Código das Wavy (C#)
├── Shared/              # Modelos e serviços partilhados (C#)
├── ComponentCreator/    # Utilitário para criar configs no MongoDB (C#)
├── configs/             # Ficheiros exemplo CSV de configuração
├── main.js              # Lógica principal da app Electron
├── renderer.js          # Lógica do frontend Electron
├── index.html           # Interface gráfica
├── styles.css           # Estilos da interface
├── package.json         # Configuração Node.js/Electron
├── validate-system.ps1  # Script PowerShell para validar dependências
```

## Tecnologias
- C# (.NET 9.0) → Liguagem principal
- RabbitMQ → Broker para Pub/Sub e suporte a RPC
- MongoDB → Armazenamento dos dados
- Electron (HTML/CSS/JavaScript) → Interface Gráfica

## Pré-requisitos
- **Node.js**
- **.NET 9.0 SDK**

## Como Correr
### Instala as dependências Node.js:
   ```powershell
   npm install
   ```

### Instala as dependências Python:
   ```powershell
   pip install -r requirements.txt
   ```

### Inicia o dashboard do Servidor Central:
   ```powershell
   python dashboard_server.py
   ```

### Iniciar a Interface Gráfica
```powershell
npm start
```
A interface Electron permite iniciar/parar componentes, ver logs e monitorizar o sistema.

> ## Servidor Central
> ### Atenção:
> 
> - Os dados *'Total Records', 'Active Wavys', 'Active Aggregators'* e *'Avg Wave Height'* não estão totalmente operacionais.
> - Embora os gráficos pareçam "estranhos", estão corretos, devido aos dados serem gerados aleatoriamente.