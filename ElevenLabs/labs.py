import sys
from elevenlabs import set_api_key, generate, stream

api_key = sys.argv[1]
voice = sys.argv[2]
model = sys.argv[3]
num_args = len(sys.argv)

text = sys.argv[4]

set_api_key(api_key)

def text_stream():
    for arg in sys.argv[4:num_args]:
        yield arg + " "
        


audio_stream = generate(
  text=text_stream(),
  voice=voice,
  model=model,
  stream=True
)

stream(audio_stream)