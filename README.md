# 🌾 Farm & Friends

Uma aplicação dedicada ao gerenciamento/simulação de atividades agrícolas e colaboração. Este projeto possui uma arquitetura dividida entre uma API robusta em .NET e um ecossistema de Frontend dinâmico.

---

## 📂 Estrutura do Projeto

O repositório está organizado da seguinte forma:

*   **`FarmAndFriends.Api/`**: Backend desenvolvido em .NET (Backend API C# + .NET 8 + Entity Framework), responsável pelas regras de negócio, persistência de dados e disponibilização dos endpoints da API.
*   **`FarmAndFriends.Frontend/`**: Interface do usuário (Client-side) que consome a API para exibição e gerenciamento dos recursos.
*   **`infra/`**: Configurações de infraestrutura, bancos de dados, containers ou scripts de deploy.
*   **`imgs/`**: Imagens e recursos visuais utilizados na documentação e no projeto.

---

## 🛠️ Tecnologias Utilizadas

*   **Backend:** .NET / C# (ASP.NET Core Web API)
*   **Frontend:** Phaser + React + Vite + Tailwind CSS
*   **Automação de Scripts:** PowerShell

---

## 🚀 Como Executar o Projeto

Para facilitar o desenvolvimento local, o repositório conta com scripts em PowerShell (`.ps1`) prontos para rodar tanto a API quanto o Frontend.

### Pré-requisitos
*   [.NET SDK](https://dotnet.microsoft.com/download) instalado.
*   PowerShell (padrão no Windows, ou instalado no macOS/Linux).

### Passo a Passo

1. **Clonar o repositório:**
   ```bash
   git clone [https://github.com/viniciusbaza/FarmAndFriends.git](https://github.com/viniciusbaza/FarmAndFriends.git)
   cd FarmAndFriends
   
2. Executar a API (Backend):
Abra um terminal PowerShell na raiz do projeto e execute:
  `./run_api.ps1`


3. Executar o Frontend:
Em um novo terminal PowerShell, execute:
  `./run_frontend.ps1`

---

## ⚙️ Infraestrutura 
(Opcional) Para apagar o volume local descartável, recriar o PostgreSQL e
reaplicar as migrations versionadas, execute na raiz do projeto:

  `./infra/database/reset-database.ps1`

Esse comando remove todos os dados locais. Ele não apaga nem regenera arquivos
de migration e não inicia a API; use `./run_api.ps1` depois do reset.

---

📄 Licença
Este projeto está sob a licença [Insira a licença aqui, ex: MIT]. Consulte o arquivo LICENSE para mais informações.
