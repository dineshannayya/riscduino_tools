# riscduino_tool
Repository for storing Arduino related tool files. This directory used along with https://github.com/dineshannayya/riscduino_board/

# Youtube Video on Arduino IDE integration #
*  https://www.youtube.com/watch?v=7_vQJfD9OVo

# Manual Setup #

## Install Arduino IDE ##

Download and install [Arduino IDE 2.2.1](https://www.arduino.cc/en/software#experimental-software) from the Arduino website. Unpack it and run their installation script as directed.

## Installation of the Riscduino Boards Package in Arduino IDE ##

### Installing Through Arduino IDE ###

This is supported for __Windows__ and __Linux__.

1. Open the Arduino-IDE.

2. Go to [File] [Preferences].
<table>
  <tr>
    <td  align="center"><img src="./docs/source/_static/Riscdunio-board-install-0.png" ></td>
  </tr>
</table>

3. Click on __Additional boards manager URLs__ and 
   Add the following link [https://github.com/dineshannayya/riscduino_tools/raw/main/package_riscduino_index.json](https://github.com/dineshannayya/riscduino_tools/raw/main/package_riscduino_index.json).  
<table>
  <tr>
    <td  align="center"><img src="./docs/source/_static/Riscduino-board-install-1.png" ></td>
    <td  align="center"><img src="./docs/source/_static/Riscduino-board-install-2.png" ></td>
  </tr>
</table>

4. Go to [Tools][Board][Board Manager] and Search for Riscduino Boards.
<table>
  <tr>
    <td  align="center"><img src="./docs/source/_static/Riscduino-board-install-3.png" ></td>
  </tr>
</table>

5. Install the latest version of Riscduino Boards.
<table>
  <tr>
    <td  align="center"><img src="./docs/source/_static/Riscduino-board-install-4.png" ></td>
  </tr>
</table>

6. Select Your Board and Port <br>
    Tools --> Board --> Riscduino Uno <br>
    Tools --> Port --> the Port in use.
 
<table>
  <tr>
    <td  align="center"><img src="./docs/source/_static/Riscduino-board-install-5.png" ></td>
  </tr>
</table>

7. Now you should be able to compile and upload code onto the Riscduino Board.

# Command Reference

*  To get SHA256 in windows 
certutil -hashfile <example> SHA256
Example: certutil -hashfile .\riscv64-unknown-elf-toolchain-10.2.0-2020.12.8-x86_64-w64-mingw32.zip SHA256


# Reference

* **Arduino IDE: Creating Custom Boards** - https://www.hackster.io/wallarug/arduino-ide-creating-custom-boards-89f7a6
* **Arduino Platform specification** - https://arduino.github.io/arduino-cli/latest/platform-specification/
* **Adding Custom (Zero based) boards to the Arduino IDE** - https://forum.arduino.cc/t/adding-custom-zero-based-boards-to-the-arduino-ide/394499/3
* **robotics-masters** - https://github.com/robotics-masters/mm1-hat-arduino
* **Arduino core extensions for FPGA-based core** -https://github.com/f32c/fpgarduino
* **FPGArduino: a cross-platform RISC-V IDE for masses** - https://riscv.org/wp-content/uploads/2016/07/Tue1600zec_fpgarduino_riscv_wsV2.pdf
* **sifive: Freedom E300 boards using the Arduino IDE** - https://github.com/sifive/cinco

