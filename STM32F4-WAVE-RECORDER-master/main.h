#include "stm32f4xx_spi.h"

#define DEFAULT_AUDIO_IN_FREQ    I2S_AudioFreq_16k

#define DECIMATION_FACTOR       64
#define SAMPLE_FREQUENCY		16000//48000  //16000//For 16kHz
#define INPUT_CHANNELS 			1
#define OUT_FREQ                SAMPLE_FREQUENCY/2
#define PDM_Input_Buffer_SIZE   (OUT_FREQ / 1000 *DECIMATION_FACTOR* INPUT_CHANNELS/8 )//(128*OUT_FREQ/DEFAULT_AUDIO_IN_FREQ *INPUT_CHANNELS)//
#define PCM_Output_Buffer_SIZE  (OUT_FREQ / 1000 * INPUT_CHANNELS)

#define Buffer_Input_SIZE		2048//PCM_Output_Buffer_SIZE  * OUT_FREQ/(DEFAULT_AUDIO_IN_FREQ/2)

//#define I2S_DIV    6
//#define I2S_ODD    1
