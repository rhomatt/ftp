using System;

namespace Project {
    class Client {

        /*
         * Parses a command and performs some logic.
         *
         * return false if quitting, true otherwise
         */
        public static bool ParseCmd(string cmd) {
            switch(cmd) {
                case "a":
                case "ascii":
                    return true;
                case "b":
                case "binary":
                    return true;
                case "cd":
                    return true;
                case "cdup":
                    return true;
                case "debug":
                    return true;
                case "ls":
                case "dir":
                    return true;
                case "get":
                    return true;
                case "?":
                case "h":
                case "help":
                    return true;
                case "passive":
                    return true;
                case "pwd":
                    return true;
                case "q":
                case "quit":
                    return false;
            }

            return true;
        }

        public static void Main(string[] args) {
            bool cont = true;

            while(cont) {
                cont = ParseCmd(Console.ReadLine());
            }
        }
    }
}
