using System;

namespace Project {
    class Program {

        /*
         * Parses a command and performs some logic.
         *
         * return false if quitting, true otherwise
         */
        public static bool ParseCmd(string cmd) {
            switch(cmd) {
                case "ascii":
                    return true;
                case "binary":
                    return true;
                case "cd":
                    return true;
                case "cdup":
                    return true;
                case "debug":
                    return true;
                case "dir":
                    return true;
                case "get":
                    return true;
                case "help":
                    return true;
                case "passive":
                    return true;
                case "pwd":
                    return true;
                case "quit":
                    return false;
            }

            return true;
        }

        public static void Main(string[] args) {

        }
    }
}
