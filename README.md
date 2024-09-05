# Server for multiplayer card game "Durak"

Backend server for the multiplayer online card game "Durak," developed using **C#** and **gRPC**. This server handles real-time game logic, player synchronization, and matchmaking for a 3-player version of the Durak card game.

![Durak](https://github.com/user-attachments/assets/bd56973c-4fc5-4603-ae0a-21ea2ddc2638)

## Client project (Unity)
- Visit https://github.com/Akezh/DurakClient-3Players/tree/main

## Key Features

- **Multiplayer Support**: Real-time communication between players using **gRPC** with duplex streaming.
- **High Performance**: Scalable backend handling thousands of concurrent games and users.
- **Game Logic**: Core game logic including card distribution, role management, and automated decision-making for gameplay.

## Technology Stack

- **C#**: Programming language used to develop the server.
- **gRPC**: Google's gRPC framework for real-time, low-latency client-server communication.
- **ASP.NET Core**: Used for player authentication and matchmaking services.
  
## Installation

1. Clone the repository:
    ```bash
    git clone https://github.com/Akezh/DurakServer-3Players.git
    ```
2. Navigate to the project directory:
    ```bash
    cd DurakServer-3Players
    ```
3. Build the solution using .NET Core:
    ```bash
    dotnet build
    ```
4. Run the server:
    ```bash
    dotnet run
    ```

## Game Logic

The backend is responsible for synchronizing the state of the game between three players in real-time. It ensures the following:

- **Random Card Distribution**: Cards are fairly shuffled and distributed to players.
- **Turn-Based Logic**: Manages player turns and validates moves.
- **Matchmaking**: Automatically matches players into game lobbies based on availability.

## Contributions

Feel free to open an issue or submit a pull request for any bugs or feature enhancements.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
