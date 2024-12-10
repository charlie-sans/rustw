/* 
File_name: main.c
Project: text 
*/
#include <stdio.h>
#include <unistd.h> // for usleep function
#include <stdlib.h> // for rand and srand functions
#include <time.h>   // for time function

// ANSI color escape codes
#define ANSI_COLOR_RED     "\x1b[31m"
#define ANSI_COLOR_GREEN   "\x1b[32m"
#define ANSI_COLOR_YELLOW  "\x1b[33m"
#define ANSI_COLOR_BLUE    "\x1b[34m"
#define ANSI_COLOR_MAGENTA "\x1b[35m"
#define ANSI_COLOR_CYAN    "\x1b[36m"
#define ANSI_COLOR_RESET   "\x1b[0m"

int main() {
    char *message = "Stutters is epic and awsome and https://soundcloud.com/Stutters";
    int i;

    // See the random number generator
    srand(time(NULL));

    for (i = 0; message[i] != '\0'; i++) {
        // Generate a random number from 0 to 5 for color selection
        int color_code = rand() % 6;
        const char *color;

        // Select color based on random number
        switch (color_code) {
            case 0: color = ANSI_COLOR_RED; break;
            case 1: color = ANSI_COLOR_GREEN; break;
            case 2: color = ANSI_COLOR_YELLOW; break;
            case 3: color = ANSI_COLOR_BLUE; break;
            case 4: color = ANSI_COLOR_MAGENTA; break;
            case 5: color = ANSI_COLOR_CYAN; break;
            default: color = ANSI_COLOR_RESET; break;
        }

        // Print the character in the selected color
        printf("%s%c", color, message[i]);
        fflush(stdout); // Ensure the character is printed immediately

        // Pause for 0.5 seconds (500000 microseconds)
        usleep(50000); // usleep is in microseconds, 500000 microseconds = 0.5 seconds
    }

    // Reset color back to normal at the end
    printf(ANSI_COLOR_RESET "\n");

    return 0;
}
