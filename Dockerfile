FROM ubuntu:latest

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && \
    apt-get install -y curl && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

RUN apt-get update && \
    apt-get install -y software-properties-common && \
    add-apt-repository ppa:dotnet/backports && \
    apt-get update && \
    apt-get install -y dotnet-sdk-8.0 && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

COPY . /root/main

EXPOSE 5107
WORKDIR /root/main/4Cows-FE

#CMD ["dotnet", "run", "4Cows-FE", "--urls", "http://0.0.0.0:5107"]
CMD ["bash"]
