/*
File_name: main.c
Project: text
*/

#include <stdio.h>
#include <stdlib.h>
#include <unistd.h> // For usleep
#include <string.h> // include this to work
#define DELAY_MICROSECONDS 5000 // 50 milliseconds

int main() {
    printf("Running fastfetch and capturing output...\n");

    // Open a pipe to capture the output of the fastfetch command
    FILE *pipe = popen("fastfetch", "r");
    if (pipe == NULL) {
        perror("Error opening pipe for fastfetch");
        return 1;
    }

    // Buffer to store the output
    char output[4096];
    size_t index = 0;

    // Read the output into the buffer
    while (fgets(output + index, sizeof(output) - index, pipe) != NULL) {
        index += strlen(output + index);
    }

    // Close the pipe
    if (pclose(pipe) == -1) {
        perror("Error closing pipe");
        return 1;
    }

    // Print the output character by character with a delay
    printf("Output from fastfetch:\n");
    for (size_t i = 0; i < index; i++) {
        putchar(output[i]);
        fflush(stdout); // Ensure the character is printed immediately
        usleep(DELAY_MICROSECONDS);
    }

    printf("\nDone.\n");
  
  return 0;
}
