FROM python:slim

RUN pip install pipenv

WORKDIR /var/app/scraper
COPY scraper/Pipfil* ./
COPY scraper/setup.py ./
WORKDIR /var/app/server
RUN ln -s /var/app/scraper scraper
COPY server/Pipfil* ./
RUN pipenv install

WORKDIR /var/app/scraper
COPY scraper .
WORKDIR /var/app/server
COPY server .
RUN rm scraper
RUN ln -s /var/app/scraper scraper

ENV PORT 5000
EXPOSE ${PORT}

CMD ["pipenv", "run", "python3", "-m", "main"]
